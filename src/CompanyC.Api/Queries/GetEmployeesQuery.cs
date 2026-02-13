using CompanyC.Api.Models;
using CompanyC.Api.Repositories;

namespace CompanyC.Api.Queries;

public record GetEmployeesQuery(int Page, int PageSize);

public record GetEmployeesResult(IReadOnlyList<Employee> Items, int TotalCount);

public interface IGetEmployeesQueryHandler
{
    ErrorOr<GetEmployeesResult> Handle(GetEmployeesQuery query);
}

public sealed class GetEmployeesQueryHandler(IEmployeeRepository repository) : IGetEmployeesQueryHandler
{
    public ErrorOr<GetEmployeesResult> Handle(GetEmployeesQuery query)
    {
        var (items, totalCount) = repository.GetAll(query.Page, query.PageSize);
        return new GetEmployeesResult(items, totalCount);
    }
}
