namespace CompanyC.Api;

internal static class QueryLoader
{
    private static readonly Dictionary<string, string> Queries;

    static QueryLoader()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "EmployeeQueries.xml");
        var doc = XDocument.Load(path);
        Queries = doc.Root!.Elements("Query")
            .ToDictionary(
                e => e.Attribute("Name")!.Value,
                e => e.Value.Trim());
    }

    internal static string Get(string name) => Queries[name];
}
