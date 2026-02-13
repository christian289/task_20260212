namespace CompanyC.Api;

public sealed class CsvEmployeeParser : IEmployeeParser
{
    private static readonly HashSet<string> KnownHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "name", "email", "tel", "phone", "joined", "joineddate"
    };

    public bool CanParse(string? contentType, string? fileExtension)
    {
        if (fileExtension is ".csv")
            return true;

        return contentType is not null
            && (contentType.Contains("csv") || contentType.Contains("text/plain"));
    }

    public List<Employee> Parse(string content)
    {
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        if (lines.Count == 0)
            return [];

        // 헤더 행 감지: 첫 줄에 알려진 필드명이 포함되어 있는지 확인
        var firstLineParts = lines[0].Split(',').Select(p => p.Trim()).ToArray();
        var hasHeader = firstLineParts.Any(p => KnownHeaders.Contains(p));

        return hasHeader
            ? ParseWithHeaders(lines, firstLineParts)
            : ParseHeuristic(lines);
    }

    private static List<Employee> ParseWithHeaders(List<string> lines, string[] headers)
    {
        var result = new List<Employee>();

        for (var i = 1; i < lines.Count; i++)
        {
            var values = lines[i].Split(',').Select(p => p.Trim()).ToArray();
            if (values.Length < 2)
                continue;

            string? name = null;
            string? email = null;
            string? phone = null;
            DateTime joinedDate = default;
            var extraFields = new Dictionary<string, string>();

            for (var j = 0; j < headers.Length && j < values.Length; j++)
            {
                var header = headers[j].ToLowerInvariant();
                var value = values[j];

                if (string.IsNullOrWhiteSpace(value))
                    continue;

                switch (header)
                {
                    case "name":
                        name = value;
                        break;
                    case "email":
                        email = value;
                        break;
                    case "tel" or "phone":
                        phone = value;
                        break;
                    case "joined" or "joineddate":
                        TryParseDate(value, out joinedDate);
                        break;
                    default:
                        extraFields[headers[j]] = value;
                        break;
                }
            }

            if (name is null || email is null || phone is null)
                continue;

            result.Add(new Employee
            {
                Name = name,
                Email = email,
                Phone = phone,
                Joined = joinedDate,
                ExtraFields = extraFields
            });
        }

        return result;
    }

    private static List<Employee> ParseHeuristic(List<string> lines)
    {
        var result = new List<Employee>();

        foreach (var line in lines)
        {
            var parts = line.Split(',')
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrEmpty(p))
                .ToArray();

            if (parts.Length < 2)
                continue;

            var name = parts[0];
            string? email = null;
            string? phone = null;
            DateTime joinedDate = default;

            var tokens = parts.Skip(1)
                .SelectMany(p => p.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrEmpty(t))
                .ToArray();

            foreach (var token in tokens)
            {
                if (token.Contains('@'))
                    email = token;
                else if (TryParseDate(token, out var date))
                    joinedDate = date;
                else if (IsPhoneNumber(token))
                    phone = token;
            }

            if (email is null || phone is null)
                continue;

            result.Add(new Employee { Name = name, Email = email, Phone = phone, Joined = joinedDate });
        }

        return result;
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

    private static bool IsPhoneNumber(string value)
    {
        return value.Replace("-", "").All(c => char.IsDigit(c) || c == '+');
    }
}
