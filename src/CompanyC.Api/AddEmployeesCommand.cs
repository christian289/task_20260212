namespace CompanyC.Api;

record AddEmployeesCommand(string Content, string? ContentType, string? FileExtension);

public interface IAddEmployeesCommandHandler
{
    List<Employee> Handle(AddEmployeesCommand command);
}

sealed class AddEmployeesCommandHandler(
    IEmployeeRepository repository,
    IEnumerable<IEmployeeParser> parsers) : IAddEmployeesCommandHandler
{
    public List<Employee> Handle(AddEmployeesCommand command)
    {
        var parser = parsers.FirstOrDefault(p => p.CanParse(command.ContentType, command.FileExtension));

        // content sniffing fallback
        if (parser is null)
        {
            var trimmed = command.Content.TrimStart();
            var inferredType = trimmed.StartsWith('[') || trimmed.StartsWith('{')
                ? "application/json"
                : "text/csv";
            parser = parsers.FirstOrDefault(p => p.CanParse(inferredType, null));
        }

        var employees = parser?.Parse(command.Content) ?? [];

        if (employees.Count > 0)
            repository.AddRange(employees);

        return employees;
    }
}
