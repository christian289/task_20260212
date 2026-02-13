namespace CompanyC.Api;

public sealed class EmployeeService : IEmployeeService
{
    private readonly IEmployeeRepository _repository;
    private readonly IEnumerable<IEmployeeParser> _parsers;

    public EmployeeService(IEmployeeRepository repository, IEnumerable<IEmployeeParser> parsers)
    {
        _repository = repository;
        _parsers = parsers;
    }

    public (IReadOnlyList<Employee> Items, int TotalCount) GetAll(int page, int pageSize)
        => _repository.GetAll(page, pageSize);

    public Employee? GetByName(string name)
        => _repository.GetByName(name);

    public void AddFromParsed(List<Employee> employees)
        => _repository.AddRange(employees);

    public List<Employee> Parse(string content, string? contentType, string? fileExtension)
    {
        var parser = _parsers.FirstOrDefault(p => p.CanParse(contentType, fileExtension));

        // content sniffing fallback
        if (parser is null)
        {
            var trimmed = content.TrimStart();
            var inferredType = trimmed.StartsWith('[') || trimmed.StartsWith('{')
                ? "application/json"
                : "text/csv";
            parser = _parsers.FirstOrDefault(p => p.CanParse(inferredType, null));
        }

        return parser?.Parse(content) ?? [];
    }
}
