using CompanyC.Api.Models;

namespace CompanyC.Api.Repositories;

public interface IEmployeeRepository
{
    (IReadOnlyList<Employee> Items, int TotalCount) GetAll(int page, int pageSize);
    Employee? GetByName(string name);
    void AddRange(List<Employee> employees);
}
