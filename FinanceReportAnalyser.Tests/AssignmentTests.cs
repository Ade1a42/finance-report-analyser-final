using Xunit;

public class AssignmentTests
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

    // ---------- helpers ----------
    private static Transaction Expense(ExpenseCategory category, decimal amount) =>
        Assert.IsType<Result<Transaction>.Ok>(ExpenseTransaction.Create(category, amount)).Value;

    private static Transaction Income(IncomeCategory category, decimal amount) =>
        Assert.IsType<Result<Transaction>.Ok>(IncomeTransaction.Create(category, amount)).Value;

    private const string Csv =
        "type,category,amount\nincome,salary,500\nexpense,Groceries,10\nexpense,Transport,20\n";

    private sealed class TrackingReader : StringReader
    {
        public bool Disposed { get; private set; }
        public TrackingReader(string text) : base(text) { }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static readonly Func<ExpenseTransaction, decimal> Strict = Scoring.CreateScore(2m);
    private static readonly Func<ExpenseTransaction, decimal> Relaxed = Scoring.CreateScore(1m);

    // ---------- REPORT 1: large expenses ----------
    [Fact]
    public void LargeExpenses_NormalData_FiltersAndOrdersByScore()
    {
        var data = new[]
        {
            Expense(ExpenseCategory.Transport, 1325m),
            Expense(ExpenseCategory.Medicine, 2000m),
            Expense(ExpenseCategory.Groceries, 50m),
            Income(IncomeCategory.Salary, 9000m)
        };

        var report = Reports.LargeExpenses(data, 1000m, Strict);

        Assert.Equal(2, report.Count);
        Assert.Equal(ExpenseCategory.Medicine, report[0].Category);
        Assert.Equal(4000m, report[0].Score);
        Assert.Equal(ExpenseCategory.Transport, report[1].Category);
    }

    [Fact]
    public void LargeExpenses_EmptyInput_ReturnsEmptyList()
    {
        Assert.Empty(Reports.LargeExpenses(new List<Transaction>(), 1000m, Strict));
    }

    [Fact]
    public void LargeExpenses_NoMatches_ReturnsEmptyList()
    {
        var data = new[] { Expense(ExpenseCategory.Groceries, 50m) };

        Assert.Empty(Reports.LargeExpenses(data, 1000m, Strict));
    }

    [Fact]
    public void LargeExpenses_TiedScores_OrderedByCategoryName()
    {
        var data = new[]
        {
            Expense(ExpenseCategory.Transport, 2000m),
            Expense(ExpenseCategory.Groceries, 2000m)
        };

        var report = Reports.LargeExpenses(data, 1000m, Strict);

        Assert.Equal(ExpenseCategory.Groceries, report[0].Category);
        Assert.Equal(ExpenseCategory.Transport, report[1].Category);
    }

    // ---------- REPORT 2: spending by category ----------
    [Fact]
    public void SpendingByCategory_GroupsAndSums()
    {
        var data = new[]
        {
            Expense(ExpenseCategory.Groceries, 10m),
            Expense(ExpenseCategory.Transport, 20m),
            Expense(ExpenseCategory.Groceries, 5m)
        };

        var report = Reports.SpendingByCategory(data);

        Assert.Equal(2, report.Count);
        Assert.Equal(new CategorySpending(ExpenseCategory.Transport, 20m, 1), report[0]);
        Assert.Equal(new CategorySpending(ExpenseCategory.Groceries, 15m, 2), report[1]);
    }

    [Fact]
    public void SpendingByCategory_EqualTotals_OrderedByCategoryName()
    {
        var data = new[]
        {
            Expense(ExpenseCategory.Transport, 10m),
            Expense(ExpenseCategory.Groceries, 10m)
        };

        var report = Reports.SpendingByCategory(data);

        Assert.Equal(ExpenseCategory.Groceries, report[0].Category);
    }

    [Fact]
    public void SpendingByCategory_EmptyInput_ReturnsEmptyList()
    {
        Assert.Empty(Reports.SpendingByCategory(new List<Transaction>()));
    }

    // ---------- EXTENSIONS ----------
    [Fact]
    public void ToMinorUnits_ConvertsTengeToTiyn()
    {
        Assert.Equal(1250L, 12.5m.ToMinorUnits());
        Assert.Equal(1L, 0.01m.ToMinorUnits());
    }

    [Fact]
    public void ExpensesAtLeast_KeepsOnlyBigExpenses_AndDoesNotChangeSource()
    {
        var data = new List<Transaction>
        {
            Expense(ExpenseCategory.Transport, 1000m),
            Expense(ExpenseCategory.Groceries, 999.99m),
            Income(IncomeCategory.Salary, 5000m)
        };

        var result = data.ExpensesAtLeast(1000m).ToList();

        Assert.Single(result);
        Assert.Equal(ExpenseCategory.Transport, result[0].Category);
        Assert.Equal(3, data.Count);
    }

    // ---------- CURRYING / PARTIAL APPLICATION ----------
    [Fact]
    public void CreateScore_StrictAndRelaxed_DifferOnUrgentCategoryOnly()
    {
        var medicine = (ExpenseTransaction)Expense(ExpenseCategory.Medicine, 100m);
        var groceries = (ExpenseTransaction)Expense(ExpenseCategory.Groceries, 100m);

        Assert.Equal(200m, Strict(medicine));
        Assert.Equal(100m, Relaxed(medicine));
        Assert.Equal(100m, Strict(groceries));
        Assert.Equal(100m, Relaxed(groceries));
    }

    // ---------- DEFERRED EXECUTION ----------
    [Fact]
    public void StreamTransactions_DoesNothingUntilEnumerated()
    {
        var opened = 0;
        Func<TextReader> open = () => { opened++; return new StringReader(Csv); };

        var lazy = LazyImporter.StreamTransactions(open);
        Assert.Equal(0, opened);

        var snapshot = lazy.ToList();
        Assert.Equal(1, opened);
        Assert.Equal(3, snapshot.Count);
    }

    [Fact]
    public void StreamTransactions_Materialised_ReuseDoesNotReopen_ButLazyReuseDoes()
    {
        var opened = 0;
        Func<TextReader> open = () => { opened++; return new StringReader(Csv); };
        var lazy = LazyImporter.StreamTransactions(open);

        var snapshot = lazy.ToList();
        _ = snapshot.Count;
        _ = snapshot.ToList();
        Assert.Equal(1, opened);

        _ = lazy.ToList();
        Assert.Equal(2, opened);
    }

    // ---------- RESOURCE LIFETIME ----------
    [Fact]
    public void StreamTransactions_FullEnumeration_DisposesReader()
    {
        var reader = new TrackingReader(Csv);

        var all = LazyImporter.StreamTransactions(() => reader).ToList();

        Assert.Equal(3, all.Count);
        Assert.True(reader.Disposed);
    }

    [Fact]
    public void StreamTransactions_EarlyStop_DisposesReader()
    {
        var reader = new TrackingReader(Csv);

        var first = LazyImporter.StreamTransactions(() => reader).Take(1).ToList();

        Assert.Single(first);
        Assert.True(reader.Disposed);
    }

    // ---------- NATIVE INTEGRATION ----------
    [Fact]
    public void SavingsRate_RealNativeCall_ReturnsManualValue()
    {
        var result = NativeFinance.SavingsRate(50000m, 10000m);

        var ok = Assert.IsType<Result<int>.Ok>(result);
        Assert.Equal(20, ok.Value);
    }

    [Fact]
    public void SavingsRate_LibraryMissing_BecomesLoadFailure()
    {
        var result = NativeFinance.SavingsRateWith(false, (_, _) => 20, 50000m, 10000m);

        var error = Assert.IsType<Result<int>.Error>(result);
        Assert.Equal("native.load_failed", error.ErrorDetails.Code);
    }

    [Fact]
    public void SavingsRate_NativeReturnsMinusOne_BecomesFailure()
    {
        var result = NativeFinance.SavingsRateWith(true, (_, _) => -1, 100m, 10m);

        var error = Assert.IsType<Result<int>.Error>(result);
        Assert.Equal("native.incorrect_input", error.ErrorDetails.Code);
    }

    [Fact]
    public void SavingsRate_InvalidDomainInput_NeverCallsNative()
    {
        var calls = 0;

        var result = NativeFinance.SavingsRateWith(true, (_, _) => { calls++; return 1; }, 100m, 500m);

        var error = Assert.IsType<Result<int>.Error>(result);
        Assert.Equal("native.invalid_input", error.ErrorDetails.Code);
        Assert.Equal(0, calls);
    }

    // ---------- COMPLETE WORKFLOWS ----------
    [Fact]
    public void Build_NormalPath_CombinesReportsAndNativeRate()
    {
        var data = new List<Transaction>
        {
            Income(IncomeCategory.Salary, 50000m),
            Expense(ExpenseCategory.Savings, 10000m),
            Expense(ExpenseCategory.Medicine, 2000m)
        };

        var result = Analysis.Build(data, 1000m, Strict, NativeFinance.SavingsRate);

        var ok = Assert.IsType<Result<AnalysisReport>.Ok>(result);
        Assert.Equal(20, ok.Value.SavingsRatePercent);
        Assert.Equal(2, ok.Value.LargeExpenses.Count);
        Assert.Equal(ExpenseCategory.Savings, ok.Value.LargeExpenses[0].Category);
    }

    [Fact]
    public void Build_NativeFailure_SkipsReportStages()
    {
        var scorerCalls = 0;
        Func<ExpenseTransaction, decimal> countingScorer = e => { scorerCalls++; return e.Amount; };
        var data = new List<Transaction> { Expense(ExpenseCategory.Medicine, 5000m) };

        var result = Analysis.Build(data, 1000m, countingScorer,
            (_, _) => Result<int>.Failure("native.load_failed", "simulated"));

        var error = Assert.IsType<Result<AnalysisReport>.Error>(result);
        Assert.Equal("native.load_failed", error.ErrorDetails.Code);
        Assert.Equal(0, scorerCalls);
    }

    [Fact]
    public void RunFromFile_NormalPath_ExportsReportFile()
    {
        var dir = NewTempDir();
        try
        {
            var input = Path.Combine(dir, "in.csv");
            var output = Path.Combine(dir, "out.txt");
            File.WriteAllText(input,
                "type,category,amount\nincome,salary,50000\nexpense,Savings,10000\nexpense,Medicine,2000\n");

            var result = Analysis.RunFromFile(input, output, 1000m, Strict);

            Assert.IsType<Result<AnalysisReport>.Ok>(result);
            Assert.True(File.Exists(output));
            Assert.Contains("Savings rate: 20%", File.ReadAllText(output));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    // ---------- FILE BOUNDARIES ----------
    [Fact]
    public void RunFromFile_MissingFile_FailsSafelyAndWritesNothing()
    {
        var dir = NewTempDir();
        try
        {
            var output = Path.Combine(dir, "out.txt");

            var result = Analysis.RunFromFile(Path.Combine(dir, "nope.csv"), output, 1000m, Strict);

            var error = Assert.IsType<Result<AnalysisReport>.Error>(result);
            Assert.Equal("file.not_found", error.ErrorDetails.Code);
            Assert.False(File.Exists(output));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void RunFromFile_MalformedFile_FailsSafelyAndWritesNothing()
    {
        var dir = NewTempDir();
        try
        {
            var input = Path.Combine(dir, "bad.csv");
            var output = Path.Combine(dir, "out.txt");
            File.WriteAllText(input, "type,category,amount\nincome,salary,500\nexpense,Medicine\n");

            var result = Analysis.RunFromFile(input, output, 1000m, Strict);

            var error = Assert.IsType<Result<AnalysisReport>.Error>(result);
            Assert.Equal("file.malformed", error.ErrorDetails.Code);
            Assert.False(File.Exists(output));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Export_UnwritablePath_ReturnsFailure()
    {
        var report = new AnalysisReport(new List<LargeExpenseLine>(), new List<CategorySpending>(), 20);
        var badPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "no_such_folder", "out.txt");

        var result = ReportExporter.Export(badPath, report);

        var error = Assert.IsType<Result<string>.Error>(result);
        Assert.Equal("export.io_error", error.ErrorDetails.Code);
    }    
}
