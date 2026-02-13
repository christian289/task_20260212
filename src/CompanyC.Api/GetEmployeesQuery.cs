namespace CompanyC.Api;

record GetEmployeesQuery(int Page, int PageSize);

record GetEmployeesResult(IReadOnlyList<Employee> Items, int TotalCount);

public interface IGetEmployeesQueryHandler
{
    GetEmployeesResult Handle(GetEmployeesQuery query);
}

sealed class GetEmployeesQueryHandler(IEmployeeRepository repository) : IGetEmployeesQueryHandler
{
    public GetEmployeesResult Handle(GetEmployeesQuery query)
    {
        var (items, totalCount) = repository.GetAll(query.Page, query.PageSize);
        return new GetEmployeesResult(items, totalCount);
    }
}
