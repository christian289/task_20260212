namespace CompanyC.Api;

public sealed class EmployeeService : IEmployeeService
{
    private readonly IEmployeeRepository _repository;
    private static readonly JsonSerializerOptions JsonReadOptions = new() { PropertyNameCaseInsensitive = true };

    public EmployeeService(IEmployeeRepository repository)
    {
        _repository = repository;
    }

    public (IReadOnlyList<Employee> Items, int TotalCount) GetAll(int page, int pageSize)
        => _repository.GetAll(page, pageSize);

    public Employee? GetByName(string name)
        => _repository.GetByName(name);

    public void AddFromParsed(List<Employee> employees)
        => _repository.AddRange(employees);

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

            if (parts.Length < 2)
                continue;

            var name = parts[0];
            string? email = null;
            string? phone = null;
            DateTime joinedDate = default;

            // 쉼표로 나눈 각 필드를 다시 공백으로 분리하여 토큰화
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

            result.Add(new Employee(name, email, phone, joinedDate));
        }

        return result;
    }

    internal static List<Employee> ParseJson(string json)
    {
        var items = JsonSerializer.Deserialize<List<JsonEmployeeDto>>(json, JsonReadOptions)
            ?? [];

        return items
            .Where(dto => !string.IsNullOrWhiteSpace(dto.Name)
                       && !string.IsNullOrWhiteSpace(dto.Email))
            .Select(dto => new Employee(
                Name: dto.Name!.Trim(),
                Email: dto.Email!.Trim(),
                Phone: dto.Tel?.Trim() ?? string.Empty,
                JoinedDate: TryParseDate(dto.Joined, out var d) ? d : default))
            .ToList();
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

    private sealed record JsonEmployeeDto(string? Name, string? Email, string? Tel, string? Joined);
}
