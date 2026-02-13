namespace CompanyC.Api;

record GetEmployeeByNameQuery(string Name);

public interface IGetEmployeeByNameQueryHandler
{
    Employee? Handle(GetEmployeeByNameQuery query);
}

sealed class GetEmployeeByNameQueryHandler(IEmployeeRepository repository) : IGetEmployeeByNameQueryHandler
{
    public Employee? Handle(GetEmployeeByNameQuery query)
        => repository.GetByName(query.Name);
}
