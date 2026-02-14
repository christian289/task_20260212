using CompanyC.Api;
using CompanyC.Api.Errors;
using CompanyC.Api.Models;
using CompanyC.Api.Repositories;

namespace CompanyC.Api.Queries;

public record GetEmployeeByNameQuery(string Name);

public interface IGetEmployeeByNameQueryHandler
{
    ErrorOr<Employee> Handle(GetEmployeeByNameQuery query);
}

public sealed class GetEmployeeByNameQueryHandler(
    IEmployeeRepository repository,
    ILogger<GetEmployeeByNameQueryHandler> logger) : IGetEmployeeByNameQueryHandler
{
    public ErrorOr<Employee> Handle(GetEmployeeByNameQuery query)
    {
        logger.NameSearchExecuting(query.Name);
        var employee = repository.GetByName(query.Name);
        if (employee is null)
        {
            logger.EmployeeNotFoundByName(query.Name);
            return EmployeeErrors.NotFound(query.Name);
        }
        return employee;
    }
}
