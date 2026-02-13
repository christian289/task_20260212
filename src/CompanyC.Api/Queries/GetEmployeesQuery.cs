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
        logger.LogDebug("DB 조회 실행: Page={Page}, PageSize={PageSize}", query.Page, query.PageSize);
        var (items, totalCount) = repository.GetAll(query.Page, query.PageSize);
        logger.LogDebug("DB 조회 완료: {ReturnedCount}/{TotalCount}건", items.Count, totalCount);
        return new GetEmployeesResult(items, totalCount);
    }
}
