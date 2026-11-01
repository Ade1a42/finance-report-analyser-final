using System.Collections.ObjectModel;
using System.Globalization;

public enum TransactionType
{
    Income,
    Expense
}

public enum ExpenseCategory
{
    Product,
    Groceries,
    EatingOutside,
    Clothes,
    Entertainment,
    Transport,
    Medicine,
    PersonalNeeds,
    UnexpectedExpenses,
    Savings,
    ReturnBorrow
}

public enum IncomeCategory
{
    Gift,
    Salary,
    Stipend,
    Borrow
}

public abstract record Transaction
{
    public decimal Amount { get; }

    protected Transaction(decimal amount)
    {
        Amount = amount;
    }

    protected static Result<decimal> ValidateAmount(decimal amount)
    {
        if (amount <= 0)
            return Result<decimal>.Failure("amount.not_positive", $"Amount {amount} must be greater than 0");

        if (decimal.Round(amount, 2) != amount)
            return Result<decimal>.Failure("amount.too_many_decimals", $"Amount {amount} must have at most 2 decimal places");

        return Result<decimal>.Success(amount);
    }
}

public sealed record IncomeTransaction : Transaction
{
    public IncomeCategory Category { get; }

    private IncomeTransaction(IncomeCategory category, decimal amount) : base(amount)
    {
        Category = category;
    }

    public static Result<Transaction> Create(IncomeCategory category, decimal amount)
    {
        if (!Enum.IsDefined(typeof(IncomeCategory), category))
            return Result<Transaction>.Failure("category.unknown", $"\"{category}\" is not an income source");

        var amountResult = ValidateAmount(amount);

        switch (amountResult)
        {
            case Result<decimal>.Ok ok:
                return Result<Transaction>.Success(new IncomeTransaction(category, ok.Value));
            case Result<decimal>.Error err:
                return Result<Transaction>.Failure(err.ErrorDetails.Code, err.ErrorDetails.Message);
            default:
                throw new InvalidOperationException();
        }
    }
}

public sealed record ExpenseTransaction : Transaction
{
    public ExpenseCategory Category { get; }

    private ExpenseTransaction(ExpenseCategory category, decimal amount) : base(amount)
    {
        Category = category;
    }

    public static Result<Transaction> Create(ExpenseCategory category, decimal amount)
    {
        if (!Enum.IsDefined(typeof(ExpenseCategory), category))
            return Result<Transaction>.Failure("category.unknown", $"\"{category}\" is not an expense category");

        var amountResult = ValidateAmount(amount);

        switch (amountResult)
        {
            case Result<decimal>.Ok ok:
                return Result<Transaction>.Success(new ExpenseTransaction(category, ok.Value));
            case Result<decimal>.Error err:
                return Result<Transaction>.Failure(err.ErrorDetails.Code, err.ErrorDetails.Message);
            default:
                throw new InvalidOperationException();
        }
    }
}

public sealed record ImportError(int LineNumber, string? RawLine, string Code, string Message);

public sealed record ImportResult(
    IReadOnlyList<Transaction> ValidTransactions,
    IReadOnlyList<ImportError> ImportErrors
);

public sealed record IncomeTotal(IncomeCategory Category, decimal Amount);
public sealed record ExpenseTotal(ExpenseCategory Category, decimal Amount);

public sealed record TransactionTotals(
    IReadOnlyDictionary<IncomeCategory, decimal> IncomeBySource,
    IReadOnlyDictionary<ExpenseCategory, decimal> ExpensesByCategory,
    decimal TotalIncome,
    decimal TotalExpenses);

public sealed record FinanceSummary(
    IReadOnlyList<IncomeTotal> IncomeBySource,
    IReadOnlyList<ExpenseTotal> ExpensesByCategory,
    decimal TotalIncome,
    decimal TotalExpenses,
    decimal Net);

public abstract record BudgetPeriod;
public sealed record OpenPeriod(IReadOnlyList<Transaction> Transactions) : BudgetPeriod;
public sealed record ClosedPeriod(FinanceSummary Summary) : BudgetPeriod;

public static class FinancePipeline
{
    public static IReadOnlyList<Transaction> SelectTransactions(
        IReadOnlyList<Transaction> transactions,
        Func<IncomeTransaction, bool> incomePolicy)
    {
        var selected = new List<Transaction>();

        foreach (var transaction in transactions)
        {
            if (transaction is ExpenseTransaction)
            {
                selected.Add(transaction);
            }
            else if (transaction is IncomeTransaction income && incomePolicy(income))
            {
                selected.Add(transaction);
            }
        }

        return Array.AsReadOnly(selected.ToArray());
    }

    public static TransactionTotals TotalTransactions(IReadOnlyList<Transaction> transactions)
    {
        var incomeBySource = new Dictionary<IncomeCategory, decimal>();
        var expensesByCategory = new Dictionary<ExpenseCategory, decimal>();
        decimal totalIncome = 0;
        decimal totalExpenses = 0;

        foreach (IncomeCategory category in Enum.GetValues<IncomeCategory>())
        {
            incomeBySource[category] = 0;
        }

        foreach (ExpenseCategory category in Enum.GetValues<ExpenseCategory>())
        {
            expensesByCategory[category] = 0;
        }

        foreach (var transaction in transactions)
        {
            if (transaction is IncomeTransaction income)
            {
                incomeBySource[income.Category] += income.Amount;
                totalIncome += income.Amount;
            }
            else if (transaction is ExpenseTransaction expense)
            {
                expensesByCategory[expense.Category] += expense.Amount;
                totalExpenses += expense.Amount;
            }
        }

        return new TransactionTotals(
            new ReadOnlyDictionary<IncomeCategory, decimal>(incomeBySource),
            new ReadOnlyDictionary<ExpenseCategory, decimal>(expensesByCategory),
            totalIncome,
            totalExpenses);
    }

    public static FinanceSummary CreateSummary(TransactionTotals totals)
    {
        var incomeLines = new List<IncomeTotal>();
        foreach (IncomeCategory category in Enum.GetValues<IncomeCategory>())
        {
            incomeLines.Add(new IncomeTotal(category, totals.IncomeBySource[category]));
        }

        var expenseLines = new List<ExpenseTotal>();
        foreach (ExpenseCategory category in Enum.GetValues<ExpenseCategory>())
        {
            expenseLines.Add(new ExpenseTotal(category, totals.ExpensesByCategory[category]));
        }

        return new FinanceSummary(
            Array.AsReadOnly(incomeLines.ToArray()),
            Array.AsReadOnly(expenseLines.ToArray()),
            totals.TotalIncome,
            totals.TotalExpenses,
            totals.TotalIncome - totals.TotalExpenses);
    }

    public static FinanceSummary Run(
        IReadOnlyList<Transaction> transactions,
        Func<IncomeTransaction, bool> incomePolicy)
    {
        var selectedTransactions = SelectTransactions(transactions, incomePolicy);
        var totals = TotalTransactions(selectedTransactions);
        return CreateSummary(totals);
    }

    public static bool AllMoneyIn(IncomeTransaction income) => true;

    public static bool EarnedOnly(IncomeTransaction income) => income.Category != IncomeCategory.Borrow;
}

public static class SummaryLookup
{
    public static Option<decimal> FindExpenseTotal(FinanceSummary summary, string categoryName)
    {
        var word = categoryName.Trim().Replace(" ", "");

        switch (TransactionParser.ParseEnum<ExpenseCategory>(word))
        {
            case Option<ExpenseCategory>.Some some:
                foreach (var line in summary.ExpensesByCategory)
                {
                    if (line.Category == some.Value)
                        return new Option<decimal>.Some(line.Amount);
                }
                return new Option<decimal>.None();

            default:
                return new Option<decimal>.None();
        }
    }
}

public static class BudgetPeriodManager
{
    public static Result<BudgetPeriod> AddTransaction(BudgetPeriod period, Transaction transaction)
    {
        return period switch
        {
            OpenPeriod open => Result<BudgetPeriod>.Success(
                new OpenPeriod(new List<Transaction>(open.Transactions) { transaction })),

            ClosedPeriod => Result<BudgetPeriod>.Failure("period.closed", "period is closed"),

            _ => throw new InvalidOperationException()
        };
    }

    public static Result<BudgetPeriod> ClosePeriod(
        BudgetPeriod period,
        Func<IncomeTransaction, bool> policy)
    {
        return period switch
        {
            OpenPeriod open => Result<BudgetPeriod>.Success(
                new ClosedPeriod(FinancePipeline.Run(open.Transactions, policy))),

            ClosedPeriod => Result<BudgetPeriod>.Failure("period.already_closed", "period already closed"),

            _ => throw new InvalidOperationException()
        };
    }
}

public sealed record LineParts(TransactionType Type, string Category, decimal Amount);

public static class TransactionParser
{
    public static Option<T> ParseEnum<T>(string word) where T : struct, Enum
    {
        if (word.Length > 0
            && char.IsLetter(word[0])
            && Enum.TryParse(word, ignoreCase: true, out T value)
            && Enum.IsDefined(value))
        {
            return new Option<T>.Some(value);
        }

        return new Option<T>.None();
    }

    public static Result<string[]> SplitLine(string? rawLine)
    {
        if (rawLine is null)
            return Result<string[]>.Failure("line.missing", "line is missing");

        if (string.IsNullOrWhiteSpace(rawLine))
            return Result<string[]>.Failure("line.empty", "line is empty");

        var parts = rawLine.Split(',');
        if (parts.Length != 3)
            return Result<string[]>.Failure("line.field_count", "expected 3 fields: type,category,amount");

        return Result<string[]>.Success(parts);
    }

    public static Result<LineParts> ReadParts(string[] fields)
    {
        TransactionType type;
        switch (ParseEnum<TransactionType>(fields[0].Trim()))
        {
            case Option<TransactionType>.Some some:
                type = some.Value;
                break;
            default:
                return Result<LineParts>.Failure("type.unknown", "type must be income or expense");
        }

        var amountText = fields[2].Trim();
        if (!decimal.TryParse(
                amountText,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out decimal amount))
        {
            return Result<LineParts>.Failure("amount.not_a_number", $"\"{amountText}\" is not a number");
        }

        var category = fields[1].Trim().Replace(" ", "");
        return Result<LineParts>.Success(new LineParts(type, category, amount));
    }

    public static Result<Transaction> CreateTransaction(LineParts parts)
    {
        if (parts.Type == TransactionType.Income)
        {
            switch (ParseEnum<IncomeCategory>(parts.Category))
            {
                case Option<IncomeCategory>.Some some:
                    return IncomeTransaction.Create(some.Value, parts.Amount);
                default:
                    return Result<Transaction>.Failure("category.unknown", $"\"{parts.Category}\" is not an income source");
            }
        }

        switch (ParseEnum<ExpenseCategory>(parts.Category))
        {
            case Option<ExpenseCategory>.Some some:
                return ExpenseTransaction.Create(some.Value, parts.Amount);
            default:
                return Result<Transaction>.Failure("category.unknown", $"\"{parts.Category}\" is not an expense category");
        }
    }

    public static Result<Transaction> ParseLine(string? rawLine)
    {
        return SplitLine(rawLine).Bind(ReadParts).Bind(CreateTransaction);
    }

    public static string Describe(Transaction transaction)
    {
        if (transaction is IncomeTransaction income)
            return $"income {income.Category} {income.Amount.ToString("F2", CultureInfo.InvariantCulture)}";

        if (transaction is ExpenseTransaction expense)
            return $"expense {expense.Category} {expense.Amount.ToString("F2", CultureInfo.InvariantCulture)}";

        throw new InvalidOperationException();
    }

    public static Result<string> ParseAndDescribe(string? rawLine)
    {
        return ParseLine(rawLine).Map(Describe);
    }

    public static ImportResult ParseLines(string[] lines, int firstLineNumber)
    {
        var validTransactions = new List<Transaction>();
        var importErrors = new List<ImportError>();
        var lineNumber = firstLineNumber;

        foreach (var rawLine in lines)
        {
            switch (ParseLine(rawLine))
            {
                case Result<Transaction>.Ok ok:
                    validTransactions.Add(ok.Value);
                    break;
                case Result<Transaction>.Error err:
                    importErrors.Add(new ImportError(lineNumber, rawLine, err.ErrorDetails.Code, err.ErrorDetails.Message));
                    break;
            }

            lineNumber++;
        }

        return new ImportResult(validTransactions, importErrors);
    }
}

public sealed record FileData(string Path, string[] Lines);

public static class FileImporter
{
    public const string Header = "type,category,amount";

    public static Result<FileData> ReadFile(string path)
    {
        try
        {
            var lines = File.ReadAllLines(path);
            return Result<FileData>.Success(new FileData(path, lines));
        }
        catch (FileNotFoundException)
        {
            return Result<FileData>.Failure("file.not_found", $"File not found: {path}");
        }
        catch (DirectoryNotFoundException)
        {
            return Result<FileData>.Failure("file.not_found", $"Folder not found: {path}");
        }
        catch (UnauthorizedAccessException)
        {
            return Result<FileData>.Failure("file.access_denied", $"Access denied: {path}");
        }
        catch (IOException ex)
        {
            return Result<FileData>.Failure("file.io_error", $"Could not read {path}: {ex.Message}");
        }
    }

    public static Result<FileData> CheckFile(FileData file)
    {
        if (file.Lines.Length == 0)
            return Result<FileData>.Failure("file.malformed", $"{file.Path}: file is empty, expected header {Header}");

        if (file.Lines[0].Trim().ToLower() != Header)
            return Result<FileData>.Failure("file.malformed", $"{file.Path}: line 1 must be the header {Header}");

        for (var i = 1; i < file.Lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(file.Lines[i]))
                continue;

            if (file.Lines[i].Split(',').Length != 3)
                return Result<FileData>.Failure("file.malformed", $"{file.Path}: line {i + 1} must have 3 fields");
        }

        return Result<FileData>.Success(file);
    }

    public static ImportResult ParseRows(FileData file)
    {
        var rows = new List<string>();
        for (var i = 1; i < file.Lines.Length; i++)
        {
            rows.Add(file.Lines[i]);
        }

        return TransactionParser.ParseLines(rows.ToArray(), 2);
    }

    public static Result<ImportResult> Import(string path)
    {
        return ReadFile(path).Bind(CheckFile).Map(ParseRows);
    }
}

public class Program
{
    private static string FormatMoney(decimal amount) =>
        amount.ToString("F2", CultureInfo.InvariantCulture) + "tg";

    private static string IncomeCategoryName(IncomeCategory category) => category switch
    {
        IncomeCategory.Gift => "gift",
        IncomeCategory.Salary => "salary",
        IncomeCategory.Stipend => "stipend",
        IncomeCategory.Borrow => "borrow",
        _ => category.ToString()
    };

    private static string ExpenseCategoryName(ExpenseCategory category) => category switch
    {
        ExpenseCategory.EatingOutside => "Eating outside",
        ExpenseCategory.PersonalNeeds => "Personal needs",
        ExpenseCategory.UnexpectedExpenses => "Unexpected expenses",
        ExpenseCategory.ReturnBorrow => "Return borrow",
        _ => category.ToString()
    };

    private static void PrintSummary(string policyName, FinanceSummary summary)
    {
        Console.WriteLine($"Your summary (policy: {policyName})");
        Console.WriteLine($"Income {FormatMoney(summary.TotalIncome)}");
        foreach (var line in summary.IncomeBySource)
        {
            Console.WriteLine($"  {IncomeCategoryName(line.Category)} {FormatMoney(line.Amount)}");
        }

        Console.WriteLine();
        Console.WriteLine($"Expenses {FormatMoney(summary.TotalExpenses)}");
        foreach (var line in summary.ExpensesByCategory)
        {
            Console.WriteLine($"  {ExpenseCategoryName(line.Category)} {FormatMoney(line.Amount)}");
        }

        Console.WriteLine();
        Console.WriteLine($"Net {FormatMoney(summary.Net)}");
    }

    private static string ResultText<T>(Result<T> result)
    {
        switch (result)
        {
            case Result<T>.Ok ok:
                return $"OK {ok.Value}";
            case Result<T>.Error err:
                return $"FAILED [{err.ErrorDetails.Code}] {err.ErrorDetails.Message}";
            default:
                throw new InvalidOperationException();
        }
    }

    private static string OptionText(Option<decimal> option)
    {
        switch (option)
        {
            case Option<decimal>.Some some:
                return $"found {FormatMoney(some.Value)}";
            case Option<decimal>.None:
                return "not found";
            default:
                throw new InvalidOperationException();
        }
    }

    static void Main()
    {
        var dataFolder = Path.Combine(AppContext.BaseDirectory, "sampleData");

        Console.WriteLine("1. Normal path");
        var imported = FileImporter.Import(Path.Combine(dataFolder, "transactions.csv"));

        switch (imported)
        {
            case Result<ImportResult>.Ok ok:
                var importResult = ok.Value;
                Console.WriteLine($"Accepted {importResult.ValidTransactions.Count} transactions; rejected {importResult.ImportErrors.Count} lines.");
                Console.WriteLine();

                var allMoneyInSummary = FinancePipeline.Run(importResult.ValidTransactions, FinancePipeline.AllMoneyIn);
                PrintSummary("AllMoneyIn", allMoneyInSummary);
                Console.WriteLine();

                var earnedOnlySummary = FinancePipeline.Run(importResult.ValidTransactions, FinancePipeline.EarnedOnly);
                PrintSummary("EarnedOnly", earnedOnlySummary);
                Console.WriteLine();

                Console.WriteLine("Rejected lines:");
                foreach (var error in importResult.ImportErrors)
                {
                    Console.WriteLine($"  line {error.LineNumber}: \"{error.RawLine}\" - [{error.Code}] {error.Message}");
                }

                Console.WriteLine();
                Console.WriteLine("2. Result chain (Bind and Map)");
                Console.WriteLine(ResultText(TransactionParser.ParseAndDescribe("income,salary,500")));

                Console.WriteLine();
                Console.WriteLine("3. Domain failures");
                Console.WriteLine("Invalid amount: " + ResultText(TransactionParser.ParseLine("income,salary,-5")));
                Console.WriteLine("Unknown item: " + ResultText(TransactionParser.ParseLine("expense,flin,4000")));

                BudgetPeriod period = new OpenPeriod(importResult.ValidTransactions);
                var closeResult = BudgetPeriodManager.ClosePeriod(period, FinancePipeline.EarnedOnly);

                switch (closeResult)
                {
                    case Result<BudgetPeriod>.Ok closed:
                        period = closed.Value;
                        Console.WriteLine("Period closed.");
                        break;
                    case Result<BudgetPeriod>.Error err:
                        Console.WriteLine($"Close failed: {err.ErrorDetails.Message}");
                        break;
                }

                var newIncome = IncomeTransaction.Create(IncomeCategory.Salary, 500m);
                switch (newIncome)
                {
                    case Result<Transaction>.Ok incomeOk:
                        Console.WriteLine("Add after close: " + ResultText(BudgetPeriodManager.AddTransaction(period, incomeOk.Value)));
                        break;
                    case Result<Transaction>.Error incomeError:
                        Console.WriteLine($"Could not create income: {incomeError.ErrorDetails.Message}");
                        break;
                }

                Console.WriteLine("Close again: " + ResultText(BudgetPeriodManager.ClosePeriod(period, FinancePipeline.EarnedOnly)));

                Console.WriteLine();
                Console.WriteLine("4. Lookups");
                Console.WriteLine("Groceries: " + OptionText(SummaryLookup.FindExpenseTotal(allMoneyInSummary, "Groceries")));
                Console.WriteLine("Savings: " + OptionText(SummaryLookup.FindExpenseTotal(allMoneyInSummary, "Savings")));
                Console.WriteLine("flin: " + OptionText(SummaryLookup.FindExpenseTotal(allMoneyInSummary, "flin")));

                var required = SummaryLookup.FindExpenseTotal(allMoneyInSummary, "flin")
                    .ToResult("category.unknown", "category flin does not exist");
                Console.WriteLine("flin (required): " + ResultText(required));
                break;

            case Result<ImportResult>.Error err:
                Console.WriteLine($"Import failed: [{err.ErrorDetails.Code}] {err.ErrorDetails.Message}");
                break;
        }

        Console.WriteLine();
        Console.WriteLine("5. File failures");
        Console.WriteLine("Missing file: " + ResultText(FileImporter.Import(Path.Combine(dataFolder, "no_such_file.csv"))));
        Console.WriteLine("Malformed file: " + ResultText(FileImporter.Import(Path.Combine(dataFolder, "malformed.csv"))));


        Console.WriteLine();
        Console.WriteLine("6. Assignment 8: lazy preview, reports, native call, export");
        var csvPath = Path.Combine(dataFolder, "transactions.csv");
        var preview = LazyImporter.StreamTransactions(LazyImporter.FromFile(csvPath)).Take(3).ToList();
        Console.WriteLine($"Streamed preview of {preview.Count} transactions (file closed after Take).");

        var scorers = new[] { ("strict", Scoring.CreateScore(2m)), ("relaxed", Scoring.CreateScore(1m)) };
        foreach (var (name, scorer) in scorers)
        {
            var exportPath = Path.Combine(AppContext.BaseDirectory, $"report_{name}.txt");
            var run = Analysis.RunFromFile(csvPath, exportPath, 1000m, scorer);
            switch (run)
            {
                case Result<AnalysisReport>.Ok ok:
                    Console.WriteLine($"-- {name} scorer --");
                    Console.WriteLine(ReportExporter.Format(ok.Value));
                    break;
                case Result<AnalysisReport>.Error err:
                    Console.WriteLine($"Failed: [{err.ErrorDetails.Code}] {err.ErrorDetails.Message}");
                    break;
            }
        }
    }
}
