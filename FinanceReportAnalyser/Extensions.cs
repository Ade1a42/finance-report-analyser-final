public static class FinanceExtensions
{
    public static long ToMinorUnits(this decimal amount) =>
        (long)decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero);

    public static IEnumerable<ExpenseTransaction> ExpensesAtLeast(
        this IEnumerable<Transaction> transactions, decimal limit) =>
        transactions.OfType<ExpenseTransaction>().Where(e => e.Amount >= limit);
}
