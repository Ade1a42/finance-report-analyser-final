using Xunit;

public class Assignment7Tests
{
    [Fact]
    public void ParseLine_ValidLine_ReturnsTransaction()
    {
        var result = TransactionParser.ParseLine("income,salary,500");

        var ok = Assert.IsType<Result<Transaction>.Ok>(result);
        var income = Assert.IsType<IncomeTransaction>(ok.Value);
        Assert.Equal(IncomeCategory.Salary, income.Category);
        Assert.Equal(500m, income.Amount);
    }

    [Fact]
    public void ParseAndDescribe_ValidLine_UsesBindAndMap()
    {
        var result = TransactionParser.ParseAndDescribe("expense,Groceries,12.5");

        var ok = Assert.IsType<Result<string>.Ok>(result);
        Assert.Equal("expense Groceries 12.50", ok.Value);
    }

    [Fact]
    public void Create_NegativeAmount_ReturnsAmountError()
    {
        var result = IncomeTransaction.Create(IncomeCategory.Salary, -5m);

        var error = Assert.IsType<Result<Transaction>.Error>(result);
        Assert.Equal("amount.not_positive", error.ErrorDetails.Code);
    }

    [Fact]
    public void Create_TooManyDecimals_ReturnsAmountError()
    {
        var result = ExpenseTransaction.Create(ExpenseCategory.Groceries, 1.234m);

        var error = Assert.IsType<Result<Transaction>.Error>(result);
        Assert.Equal("amount.too_many_decimals", error.ErrorDetails.Code);
    }

    [Fact]
    public void ParseLine_UnknownCategory_ReturnsCategoryError()
    {
        var result = TransactionParser.ParseLine("expense,flin,4000");

        var error = Assert.IsType<Result<Transaction>.Error>(result);
        Assert.Equal("category.unknown", error.ErrorDetails.Code);
        Assert.True(error.ErrorDetails.Message.Contains("flin"));
    }

    [Fact]
    public void AddTransaction_ClosedPeriod_ReturnsPeriodError()
    {
        var transactions = new List<Transaction>();
        BudgetPeriod open = new OpenPeriod(transactions);
        var closeResult = BudgetPeriodManager.ClosePeriod(open, FinancePipeline.AllMoneyIn);
        var closed = Assert.IsType<Result<BudgetPeriod>.Ok>(closeResult).Value;

        var income = Assert.IsType<Result<Transaction>.Ok>(IncomeTransaction.Create(IncomeCategory.Salary, 100m)).Value;
        var addResult = BudgetPeriodManager.AddTransaction(closed, income);

        var error = Assert.IsType<Result<BudgetPeriod>.Error>(addResult);
        Assert.Equal("period.closed", error.ErrorDetails.Code);
    }

    [Fact]
    public void AddTransaction_OpenPeriod_DoesNotChangeOldPeriod()
    {
        var transactions = new List<Transaction>();
        var open = new OpenPeriod(transactions);
        var income = Assert.IsType<Result<Transaction>.Ok>(IncomeTransaction.Create(IncomeCategory.Salary, 100m)).Value;

        var addResult = BudgetPeriodManager.AddTransaction(open, income);

        var newPeriod = Assert.IsType<OpenPeriod>(Assert.IsType<Result<BudgetPeriod>.Ok>(addResult).Value);
        Assert.Equal(0, open.Transactions.Count);
        Assert.Equal(1, newPeriod.Transactions.Count);
    }

    [Fact]
    public void Lookup_CategoryWithZeroTotal_IsFoundWithZero()
    {
        var summary = FinancePipeline.Run(new List<Transaction>(), FinancePipeline.AllMoneyIn);

        var found = SummaryLookup.FindExpenseTotal(summary, "Savings");

        var some = Assert.IsType<Option<decimal>.Some>(found);
        Assert.Equal(0m, some.Value);
    }

    [Fact]
    public void Lookup_UnknownCategory_IsNotFound()
    {
        var summary = FinancePipeline.Run(new List<Transaction>(), FinancePipeline.AllMoneyIn);

        var found = SummaryLookup.FindExpenseTotal(summary, "flin");

        Assert.IsType<Option<decimal>.None>(found);
    }

    [Fact]
    public void ToResult_FoundZero_IsOk_AndNotFound_IsError()
    {
        Option<int> zero = new Option<int>.Some(0);
        Option<int> nothing = new Option<int>.None();

        var okResult = zero.ToResult("missing", "no value");
        var errorResult = nothing.ToResult("missing", "no value");

        Assert.Equal(0, Assert.IsType<Result<int>.Ok>(okResult).Value);
        Assert.Equal("missing", Assert.IsType<Result<int>.Error>(errorResult).ErrorDetails.Code);
    }

    [Fact]
    public void Map_OnOk_ChangesValue()
    {
        var result = Result<int>.Success(3).Map(x => x * 2);

        var ok = Assert.IsType<Result<int>.Ok>(result);
        Assert.Equal(6, ok.Value);
    }

    [Fact]
    public void Bind_OnOk_ReturnsNextResult()
    {
        var okResult = Result<int>.Success(10).Bind(x => Result<int>.Success(x + 1));
        var failResult = Result<int>.Success(10).Bind(x => Result<int>.Failure("next.failed", "next failed"));

        Assert.Equal(11, Assert.IsType<Result<int>.Ok>(okResult).Value);
        Assert.Equal("next.failed", Assert.IsType<Result<int>.Error>(failResult).ErrorDetails.Code);
    }

    [Fact]
    public void Map_OnError_KeepsErrorAndDoesNotCallFunction()
    {
        var calls = 0;

        var result = Result<int>.Failure("bad", "bad input").Map(x =>
        {
            calls++;
            return x * 2;
        });

        Assert.Equal(0, calls);
        Assert.Equal("bad", Assert.IsType<Result<int>.Error>(result).ErrorDetails.Code);
    }

    [Fact]
    public void Bind_OnError_KeepsErrorAndDoesNotCallFunction()
    {
        var calls = 0;

        var result = Result<int>.Failure("bad", "bad input").Bind(x =>
        {
            calls++;
            return Result<int>.Success(x);
        });

        Assert.Equal(0, calls);
        Assert.Equal("bad", Assert.IsType<Result<int>.Error>(result).ErrorDetails.Code);
    }

    [Fact]
    public void BindChain_FailureInStageTwo_SkipsStageThree()
    {
        var stageThreeCalls = 0;

        var result = Result<int>.Success(1)
            .Bind(x => Result<int>.Success(x + 1))
            .Bind(x => Result<int>.Failure("stage2.failed", "stage 2 failed"))
            .Bind(x =>
            {
                stageThreeCalls++;
                return Result<int>.Success(x);
            });

        Assert.Equal(0, stageThreeCalls);
        Assert.Equal("stage2.failed", Assert.IsType<Result<int>.Error>(result).ErrorDetails.Code);
    }

    [Fact]
    public void Import_MissingFile_ReturnsFileNotFound()
    {
        var path = Path.Combine(Path.GetTempPath(), "no_such_file_" + Guid.NewGuid() + ".csv");

        var result = FileImporter.Import(path);

        var error = Assert.IsType<Result<ImportResult>.Error>(result);
        Assert.Equal("file.not_found", error.ErrorDetails.Code);
        Assert.True(error.ErrorDetails.Message.Contains("no_such_file_"));
    }

    [Fact]
    public void Import_RowWithTwoFields_ReturnsMalformed()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(path, new[] { "type,category,amount", "income,salary,100", "expense,Medicine" });

            var result = FileImporter.Import(path);

            var error = Assert.IsType<Result<ImportResult>.Error>(result);
            Assert.Equal("file.malformed", error.ErrorDetails.Code);
            Assert.True(error.ErrorDetails.Message.Contains("line 3"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Import_WrongHeader_ReturnsMalformed()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(path, new[] { "income,salary,100" });

            var result = FileImporter.Import(path);

            var error = Assert.IsType<Result<ImportResult>.Error>(result);
            Assert.Equal("file.malformed", error.ErrorDetails.Code);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Import_ValidFile_KeepsGoodRowsAndReportsBadRow()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(path, new[] { "type,category,amount", "income,salary,100", "expense,flin,5", "expense,Groceries,7.5" });

            var result = FileImporter.Import(path);

            var importResult = Assert.IsType<Result<ImportResult>.Ok>(result).Value;
            Assert.Equal(2, importResult.ValidTransactions.Count);
            Assert.Equal(1, importResult.ImportErrors.Count);
            Assert.Equal(3, importResult.ImportErrors[0].LineNumber);
            Assert.Equal("category.unknown", importResult.ImportErrors[0].Code);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
