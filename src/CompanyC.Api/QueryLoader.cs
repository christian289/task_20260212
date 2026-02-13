namespace CompanyC.Api;

internal static class QueryLoader
{
    private static readonly Dictionary<string, string> Queries;

    static QueryLoader()
    {
        var assembly = typeof(QueryLoader).Assembly;
        using var stream = assembly.GetManifestResourceStream("CompanyC.Api.EmployeeQueries.xml")
            ?? throw new InvalidOperationException("Embedded resource 'EmployeeQueries.xml' not found.");

        var doc = XDocument.Load(stream);
        Queries = doc.Root!.Elements("Query")
            .ToDictionary(
                e => e.Attribute("Name")!.Value,
                e => e.Value.Trim());
    }

    internal static string Get(string name) => Queries[name];
}
