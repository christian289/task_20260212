namespace CompanyC.Api;

public record GetEmployeeByNameQuery(string Name);

public interface IGetEmployeeByNameQueryHandler
{
    Employee? Handle(GetEmployeeByNameQuery query);
}

public sealed class GetEmployeeByNameQueryHandler(IEmployeeRepository repository) : IGetEmployeeByNameQueryHandler
{
    public Employee? Handle(GetEmployeeByNameQuery query)
        => repository.GetByName(query.Name);
}
