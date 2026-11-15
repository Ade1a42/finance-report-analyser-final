using System.Runtime.InteropServices;

internal static class Native
{
    public static string FileName =>
        OperatingSystem.IsWindows() ? "finance.dll"
        : OperatingSystem.IsMacOS() ? "libfinance.dylib"
        : "libfinance.so";

    public static readonly string LibPath =
        Path.Combine(AppContext.BaseDirectory, "runtime", FileName);

    public static bool IsAvailable() => File.Exists(LibPath);

    static Native()
    {
        NativeLibrary.SetDllImportResolver(typeof(Native).Assembly, (name, _, _) =>
            name == "finance" && File.Exists(LibPath)
                ? NativeLibrary.Load(LibPath)
                : IntPtr.Zero);
    }

    [DllImport("finance", CallingConvention = CallingConvention.Cdecl)]
    public static extern int savings_rate(long incomeMinor, long savingsMinor);
}

public static class NativeFinance
{
    public static bool LibraryAvailable => Native.IsAvailable();
    public static Result<int> SavingsRate(decimal income, decimal savings) =>
        SavingsRateWith(Native.IsAvailable(), Native.savings_rate, income, savings);

    public static Result<int> SavingsRateWith(
        bool libraryAvailable, Func<long, long, int> nativeCall, decimal income, decimal savings)
    {
        if (income <= 0 || savings < 0 || savings > income)
            return Result<int>.Failure("native.invalid_input", "Income must be above 0 and savings between 0 and income");

        if (!libraryAvailable)
            return Result<int>.Failure("native.load_failed", $"{Native.FileName} not found in the runtime folder");

        try
        {
            return nativeCall(income.ToMinorUnits(), savings.ToMinorUnits()) switch
            {
                -1 => Result<int>.Failure("native.incorrect_input", "Native library rejected the values"),
                var percent => Result<int>.Success(percent)
            };
        }
        catch (EntryPointNotFoundException ex)
        {
            return Result<int>.Failure("native.entry_missing", $"Native function not found: {ex.Message}");
        }
        catch (BadImageFormatException ex)
        {
            return Result<int>.Failure("native.wrong_architecture", $"Wrong library architecture: {ex.Message}");
        }
        catch (DllNotFoundException ex)
        {
            return Result<int>.Failure("native.load_failed", $"Native library could not be loaded: {ex.Message}");
        }
    }
}