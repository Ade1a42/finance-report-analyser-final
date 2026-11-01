public static class Scoring
{
    public static readonly Func<decimal, Func<ExpenseTransaction, decimal>> CreateScore =
        weight => expense => IsUrgent(expense.Category) ? expense.Amount * weight : expense.Amount;

    private static bool IsUrgent(ExpenseCategory category) =>
        category is ExpenseCategory.Medicine or ExpenseCategory.UnexpectedExpenses;
}
