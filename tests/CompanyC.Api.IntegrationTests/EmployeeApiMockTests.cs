using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CompanyC.Api;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CompanyC.Api.IntegrationTests;

public sealed class EmployeeApiMockTests : IDisposable
{
    private readonly Mock<IEmployeeService> _mockService = new();
    private readonly HttpClient _client;
    private readonly WebApplicationFactory<Program> _factory;
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public EmployeeApiMockTests()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Remove existing registrations
                    var descriptors = services
                        .Where(d => d.ServiceType == typeof(IEmployeeService)
                                 || d.ServiceType == typeof(EmployeeService))
                        .ToList();
                    foreach (var d in descriptors)
                        services.Remove(d);

                    // Register mock
                    services.AddSingleton(_mockService.Object);
                });
            });
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task GetEmployees_CallsServiceWithCorrectPagination()
    {
        var employees = new List<Employee>
        {
            new() { Name = "김테스트", Email = "test@test.com", Phone = "01012345678", JoinedDate = new DateTime(2020, 1, 1) }
        };
        _mockService.Setup(s => s.GetAll(2, 5))
            .Returns((employees.AsReadOnly() as IReadOnlyList<Employee>, 11));

        var response = await _client.GetAsync("/api/employee?page=2&pageSize=5");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _mockService.Verify(s => s.GetAll(2, 5), Times.Once);
    }

    [Fact]
    public async Task GetEmployeeByName_ReturnsNotFound_WhenServiceReturnsNull()
    {
        _mockService.Setup(s => s.GetByName("없는사람"))
            .Returns((Employee?)null);

        var response = await _client.GetAsync("/api/employee/없는사람");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        _mockService.Verify(s => s.GetByName("없는사람"), Times.Once);
    }

    [Fact]
    public async Task GetEmployeeByName_ReturnsOk_WhenServiceReturnsEmployee()
    {
        var employee = new Employee
        {
            Name = "박모크",
            Email = "mock@test.com",
            Phone = "01099998888",
            JoinedDate = new DateTime(2022, 6, 15)
        };
        _mockService.Setup(s => s.GetByName("박모크"))
            .Returns(employee);

        var response = await _client.GetAsync("/api/employee/박모크");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(), _json);
        Assert.Equal("박모크", body.GetProperty("name").GetString());
        Assert.Equal("mock@test.com", body.GetProperty("email").GetString());
        _mockService.Verify(s => s.GetByName("박모크"), Times.Once);
    }

    [Fact]
    public async Task PostCsv_CallsAddFromParsed_WithParsedData()
    {
        _mockService.Setup(s => s.AddFromParsed(It.IsAny<List<Employee>>()));

        var csv = "김파싱, parse@test.com 01011112222, 2020.05.01";
        var content = new StringContent(csv, Encoding.UTF8, "text/csv");

        var response = await _client.PostAsync("/api/employee", content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        _mockService.Verify(s => s.AddFromParsed(It.Is<List<Employee>>(list =>
            list.Count == 1 &&
            list[0].Name == "김파싱" &&
            list[0].Email == "parse@test.com" &&
            list[0].Phone == "01011112222"
        )), Times.Once);
    }
}
