using System.Globalization;

public sealed record AnalysisReport(
    IReadOnlyList<LargeExpenseLine> LargeExpenses,
    IReadOnlyList<CategorySpending> Spending,
    int SavingsRatePercent);

public static class Analysis
{
    public static Result<AnalysisReport> Build(
        IReadOnlyList<Transaction> transactions,
        decimal limit,
        Func<ExpenseTransaction, decimal> scorer,
        Func<decimal, decimal, Result<int>> savingsRate)
    {
        var income = Reports.TotalIncome(transactions);
        var savings = Reports.TotalSavings(transactions);

        return savingsRate(income, savings).Map(percent => new AnalysisReport(
            Reports.LargeExpenses(transactions, limit, scorer),
            Reports.SpendingByCategory(transactions),
            percent));
    }

    public static Result<AnalysisReport> RunFromFile(
        string path, string exportPath, decimal limit, Func<ExpenseTransaction, decimal> scorer)
    {
        return FileImporter.Import(path)
            .Bind(import => Build(import.ValidTransactions, limit, scorer, NativeFinance.SavingsRate))
            .Bind(report => ReportExporter.Export(exportPath, report).Map(_ => report));
    }
}

public static class ReportExporter
{
    private static string Money(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);

    public static string Format(AnalysisReport report)
    {
        var lines = new List<string> { "Large expenses (by score):" };
        lines.AddRange(report.LargeExpenses.Select(l => $"  {l.Category}: {Money(l.Amount)} (score {Money(l.Score)})"));
        lines.Add("Spending by category:");
        lines.AddRange(report.Spending.Select(s => $"  {s.Category}: {Money(s.Total)} in {s.Count} transaction(s)"));
        lines.Add($"Savings rate: {report.SavingsRatePercent}%");
        return string.Join(Environment.NewLine, lines);
    }

    public static Result<string> Export(string path, AnalysisReport report)
    {
        try
        {
            using var writer = new StreamWriter(path);
            writer.Write(Format(report));
            return Result<string>.Success(path);
        }
        catch (UnauthorizedAccessException)
        {
            return Result<string>.Failure("export.access_denied", $"Access denied: {path}");
        }
        catch (IOException ex)
        {
            return Result<string>.Failure("export.io_error", $"Could not write {path}: {ex.Message}");
        }
    }
}