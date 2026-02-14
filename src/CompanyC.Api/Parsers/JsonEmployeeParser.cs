using CompanyC.Api.Errors;
using CompanyC.Api.Models;

namespace CompanyC.Api.Parsers;

public sealed class JsonEmployeeParser(ILogger<JsonEmployeeParser> logger) : IEmployeeParser
{
    private static readonly JsonSerializerOptions JsonReadOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly HashSet<string> KnownKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "name", "email", "tel", "joined"
    };

    public bool CanParse(string? contentType, string? fileExtension)
    {
        if (fileExtension is ".json")
            return true;

        return contentType is not null && contentType.Contains("json");
    }

    public ErrorOr<List<Employee>> Parse(string content)
    {
        try
        {
            logger.JsonParsingStarted(content.Length);

            var items = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(content, JsonReadOptions)
                ?? [];

            var result = new List<Employee>();

            foreach (var rawItem in items)
            {
                // case-insensitive Dictionary로 변환하여 O(1) 탐색
                var item = new Dictionary<string, JsonElement>(rawItem, StringComparer.OrdinalIgnoreCase);

                var name = GetString(item, "name");
                var email = GetString(item, "email");

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email))
                    continue;

                var tel = GetString(item, "tel") ?? string.Empty;
                var joined = GetString(item, "joined");

                var extraFields = new Dictionary<string, string>();
                foreach (var kvp in item)
                {
                    if (!KnownKeys.Contains(kvp.Key))
                        extraFields[kvp.Key] = kvp.Value.ToString();
                }

                result.Add(new Employee
                {
                    Name = name.Trim(),
                    Email = email.Trim(),
                    Tel = tel.Trim(),
                    Joined = DateParsingHelper.TryParseDate(joined, out var d) ? d : default,
                    ExtraFields = extraFields
                });
            }

            logger.JsonParsingCompleted(result.Count);
            return result;
        }
        catch (JsonException ex)
        {
            logger.JsonParsingError(ex);
            return EmployeeErrors.ParseFailed("JSON", ex.Message);
        }
        catch (Exception ex)
        {
            logger.JsonParsingError(ex);
            return EmployeeErrors.ParseFailed("JSON", ex.Message);
        }
    }

    private static string? GetString(Dictionary<string, JsonElement> item, string key)
    {
        if (!item.TryGetValue(key, out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
    }
}
