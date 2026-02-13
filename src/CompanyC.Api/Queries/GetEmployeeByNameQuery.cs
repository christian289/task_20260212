using CompanyC.Api.Errors;
using CompanyC.Api.Models;
using CompanyC.Api.Repositories;

namespace CompanyC.Api.Queries;

public record GetEmployeeByNameQuery(string Name);

public interface IGetEmployeeByNameQueryHandler
{
    ErrorOr<Employee> Handle(GetEmployeeByNameQuery query);
}

public sealed class GetEmployeeByNameQueryHandler(IEmployeeRepository repository) : IGetEmployeeByNameQueryHandler
{
    public ErrorOr<Employee> Handle(GetEmployeeByNameQuery query)
    {
        var employee = repository.GetByName(query.Name);
        return employee is not null
            ? employee
            : EmployeeErrors.NotFound(query.Name);
    }
}
