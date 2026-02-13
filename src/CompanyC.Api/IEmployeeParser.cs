namespace CompanyC.Api;

public interface IEmployeeParser
{
    bool CanParse(string? contentType, string? fileExtension);
    List<Employee> Parse(string content);
}
