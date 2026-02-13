namespace CompanyC.Api;

public sealed class CsvEmployeeParser : IEmployeeParser
{
    public bool CanParse(string? contentType, string? fileExtension)
    {
        if (fileExtension is ".csv")
            return true;

        return contentType is not null
            && (contentType.Contains("csv") || contentType.Contains("text/plain"));
    }

    public List<Employee> Parse(string content)
    {
        var result = new List<Employee>();
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            var parts = trimmed.Split(',')
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

            result.Add(new Employee { Name = name, Email = email, Phone = phone, JoinedDate = joinedDate });
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
