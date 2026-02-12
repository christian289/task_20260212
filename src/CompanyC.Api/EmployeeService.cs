using System.Globalization;
using System.Text.Json;

namespace CompanyC.Api;

public sealed class EmployeeService
{
    private readonly List<Employee> _employees = [];

    public (IReadOnlyList<Employee> Items, int TotalCount) GetAll(int page, int pageSize)
    {
        var total = _employees.Count;
        var items = _employees
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();
        return (items, total);
    }

    public Employee? GetByName(string name)
    {
        return _employees.FirstOrDefault(e =>
            e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public List<Employee> AddFromCsv(string csv)
    {
        var employees = ParseCsv(csv);
        _employees.AddRange(employees);
        return employees;
    }

    public List<Employee> AddFromJson(string json)
    {
        var employees = ParseJson(json);
        _employees.AddRange(employees);
        return employees;
    }

    public void AddFromParsed(List<Employee> employees)
    {
        _employees.AddRange(employees);
    }

    internal static List<Employee> ParseCsv(string csv)
    {
        var result = new List<Employee>();
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            var parts = trimmed.Split(',')
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrEmpty(p))
                .ToArray();

            if (parts.Length < 3)
                continue;

            var name = parts[0];
            string? email = null;
            string? phone = null;
            DateTime joinedDate = default;

            foreach (var part in parts.Skip(1))
            {
                if (part.Contains('@'))
                    email = part;
                else if (TryParseDate(part, out var date))
                    joinedDate = date;
                else if (IsPhoneNumber(part))
                    phone = part;
            }

            if (email is null || phone is null)
                continue;

            result.Add(new Employee
            {
                Name = name,
                Email = email,
                Phone = phone,
                JoinedDate = joinedDate
            });
        }

        return result;
    }

    internal static List<Employee> ParseJson(string json)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var items = JsonSerializer.Deserialize<List<JsonEmployeeDto>>(json, options)
            ?? [];

        return items.Select(dto => new Employee
        {
            Name = dto.Name ?? string.Empty,
            Email = dto.Email ?? string.Empty,
            Phone = dto.Tel ?? string.Empty,
            JoinedDate = TryParseDate(dto.Joined, out var d) ? d : default
        }).ToList();
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

    private sealed class JsonEmployeeDto
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Tel { get; set; }
        public string? Joined { get; set; }
    }
}
