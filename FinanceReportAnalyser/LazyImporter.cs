public static class LazyImporter
{
    public static Func<TextReader> FromFile(string path) => () => new StreamReader(path);

    public static IEnumerable<Transaction> StreamTransactions(Func<TextReader> openReader)
    {
        using var reader = openReader();
        reader.ReadLine(); // skip the header line

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            if (TransactionParser.ParseLine(line) is Result<Transaction>.Ok ok)
                yield return ok.Value;
        }
    }
}