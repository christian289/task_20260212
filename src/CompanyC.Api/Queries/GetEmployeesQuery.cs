using CompanyC.Api.Models;
using CompanyC.Api.Repositories;

namespace CompanyC.Api.Queries;

public record GetEmployeesQuery(int Page, int PageSize);

public record GetEmployeesResult(IReadOnlyList<Employee> Items, int TotalCount);

public interface IGetEmployeesQueryHandler
{
    GetEmployeesResult Handle(GetEmployeesQuery query);
}

public sealed class GetEmployeesQueryHandler(IEmployeeRepository repository) : IGetEmployeesQueryHandler
{
    public GetEmployeesResult Handle(GetEmployeesQuery query)
    {
        var (items, totalCount) = repository.GetAll(query.Page, query.PageSize);
        return new GetEmployeesResult(items, totalCount);
    }
}
