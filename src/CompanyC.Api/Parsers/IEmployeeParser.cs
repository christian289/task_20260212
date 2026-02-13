using CompanyC.Api.Models;

namespace CompanyC.Api.Parsers;

public interface IEmployeeParser
{
    bool CanParse(string? contentType, string? fileExtension);
    List<Employee> Parse(string content);
}
