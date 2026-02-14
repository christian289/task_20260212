using CompanyC.Api.Errors;
using CompanyC.Api.Models;
using CompanyC.Api.Repositories;

namespace CompanyC.Api.Queries;

public record GetEmployeesQuery(int Page, int PageSize);

public record GetEmployeesResult(IReadOnlyList<Employee> Items, int TotalCount);

public interface IGetEmployeesQueryHandler
{
    ErrorOr<GetEmployeesResult> Handle(GetEmployeesQuery query);
}

public sealed class GetEmployeesQueryHandler(
    IEmployeeRepository repository,
    ILogger<GetEmployeesQueryHandler> logger) : IGetEmployeesQueryHandler
{
    public ErrorOr<GetEmployeesResult> Handle(GetEmployeesQuery query)
    {
        try
        {
            logger.DbQueryExecuting(query.Page, query.PageSize);
            var (items, totalCount) = repository.GetAll(query.Page, query.PageSize);
            logger.DbQueryCompleted(items.Count, totalCount);
            return new GetEmployeesResult(items, totalCount);
        }
        catch (Exception ex)
        {
            logger.StorageError(ex);
            return EmployeeErrors.StorageFailed;
        }
    }
}
