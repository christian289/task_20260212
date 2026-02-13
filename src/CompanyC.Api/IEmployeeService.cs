namespace CompanyC.Api;

public interface IEmployeeService
{
    (IReadOnlyList<Employee> Items, int TotalCount) GetAll(int page, int pageSize);
    Employee? GetByName(string name);
    void AddFromParsed(List<Employee> employees);
    List<Employee> Parse(string content, string? contentType, string? fileExtension);
}
