using CompanyC.Api.Errors;
using CompanyC.Api.Models;
using CompanyC.Api.Parsers;
using CompanyC.Api.Repositories;

namespace CompanyC.Api.Commands;

public record AddEmployeesCommand(string Content, string? ContentType, string? FileExtension);

public interface IAddEmployeesCommandHandler
{
    ErrorOr<List<Employee>> Handle(AddEmployeesCommand command);
}

public sealed class AddEmployeesCommandHandler(
    IEmployeeRepository repository,
    IEnumerable<IEmployeeParser> parsers,
    IValidator<Employee> employeeValidator) : IAddEmployeesCommandHandler
{
    public ErrorOr<List<Employee>> Handle(AddEmployeesCommand command)
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

        if (parser is null)
            return EmployeeErrors.NoParserFound;

        var parseResult = parser.Parse(command.Content);
        if (parseResult.IsError)
            return parseResult.Errors;

        var parsed = parseResult.Value;
        if (parsed.Count == 0)
            return EmployeeErrors.NoValidData;

        // FluentValidation: 각 Employee 검증
        List<Error> validationErrors = [];
        List<Employee> validEmployees = [];

        for (var i = 0; i < parsed.Count; i++)
        {
            var result = employeeValidator.Validate(parsed[i]);
            if (result.IsValid)
            {
                validEmployees.Add(parsed[i]);
            }
            else
            {
                validationErrors.AddRange(result.Errors.Select(e =>
                    Error.Validation(
                        code: $"Employee[{i}].{e.PropertyName}",
                        description: e.ErrorMessage)));
            }
        }

        // 유효한 직원이 하나도 없으면 전체 검증 에러 반환
        if (validEmployees.Count == 0)
            return validationErrors;

        var storeResult = repository.AddRange(validEmployees);
        if (storeResult.IsError)
            return storeResult.Errors;

        return validEmployees;
    }
}
