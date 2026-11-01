public sealed record LargeExpenseLine(ExpenseCategory Category, decimal Amount, decimal Score);
public sealed record CategorySpending(ExpenseCategory Category, decimal Total, int Count);

public static class Reports
{
    public static IReadOnlyList<LargeExpenseLine> LargeExpenses(
        IEnumerable<Transaction> transactions, decimal limit, Func<ExpenseTransaction, decimal> scorer) =>
        transactions
            .ExpensesAtLeast(limit)
            .Select(e => new LargeExpenseLine(e.Category, e.Amount, scorer(e)))
            .OrderByDescending(line => line.Score)
            .ThenBy(line => line.Category.ToString())
            .ToList();

    public static IReadOnlyList<CategorySpending> SpendingByCategory(IEnumerable<Transaction> transactions) =>
        transactions
            .OfType<ExpenseTransaction>()
            .GroupBy(e => e.Category)
            .Select(g => new CategorySpending(g.Key, g.Sum(e => e.Amount), g.Count()))
            .OrderByDescending(c => c.Total)
            .ThenBy(c => c.Category.ToString())
            .ToList();

    public static decimal TotalIncome(IEnumerable<Transaction> transactions) =>
        transactions.OfType<IncomeTransaction>().Sum(i => i.Amount);

    public static decimal TotalSavings(IEnumerable<Transaction> transactions) =>
        transactions.OfType<ExpenseTransaction>()
            .Where(e => e.Category == ExpenseCategory.Savings)
            .Sum(e => e.Amount);
}