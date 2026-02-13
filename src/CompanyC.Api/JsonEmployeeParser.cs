namespace CompanyC.Api;

public sealed class JsonEmployeeParser : IEmployeeParser
{
    private static readonly JsonSerializerOptions JsonReadOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly string[] KnownKeys = ["name", "email", "tel", "joined"];

    public bool CanParse(string? contentType, string? fileExtension)
    {
        if (fileExtension is ".json")
            return true;

        return contentType is not null && contentType.Contains("json");
    }

    public List<Employee> Parse(string content)
    {
        var items = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(content, JsonReadOptions)
            ?? [];

        var result = new List<Employee>();

        foreach (var item in items)
        {
            var name = GetString(item, "name");
            var email = GetString(item, "email");

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email))
                continue;

            var tel = GetString(item, "tel") ?? string.Empty;
            var joined = GetString(item, "joined");

            var extraFields = new Dictionary<string, string>();
            foreach (var kvp in item)
            {
                if (!KnownKeys.Contains(kvp.Key.ToLowerInvariant()))
                    extraFields[kvp.Key] = kvp.Value.ToString();
            }

            result.Add(new Employee
            {
                Name = name.Trim(),
                Email = email.Trim(),
                Phone = tel.Trim(),
                JoinedDate = TryParseDate(joined, out var d) ? d : default,
                ExtraFields = extraFields
            });
        }

        return result;
    }

    private static string? GetString(Dictionary<string, JsonElement> item, string key)
    {
        var match = item.FirstOrDefault(kvp => kvp.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        return match.Key is not null && match.Value.ValueKind == JsonValueKind.String
            ? match.Value.GetString()
            : null;
    }

    private static bool TryParseDate(string? value, out DateTime result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = default;
            return false;
        }

        string[] formats = ["yyyy-MM-dd", "yyyy.MM.dd", "yyyy/MM/dd"];
        return DateTime.TryParseExact(value.Trim(), formats,
            CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
    }
}
