namespace CompanyC.Api.IntegrationTests;

public sealed class EmployeeApiMockTests : IDisposable
{
    private readonly Mock<IGetEmployeesQueryHandler> _mockGetEmployees = new();
    private readonly Mock<IGetEmployeeByNameQueryHandler> _mockGetByName = new();
    private readonly Mock<IAddEmployeesCommandHandler> _mockAddEmployees = new();
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public EmployeeApiMockTests()
    {
        _factory = new TestWebApplicationFactory();
        var factoryWithMock = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove existing registrations
                var descriptors = services
                    .Where(d => d.ServiceType == typeof(IGetEmployeesQueryHandler)
                             || d.ServiceType == typeof(IGetEmployeeByNameQueryHandler)
                             || d.ServiceType == typeof(IAddEmployeesCommandHandler))
                    .ToList();
                foreach (var d in descriptors)
                    services.Remove(d);

                // Register mocks
                services.AddSingleton(_mockGetEmployees.Object);
                services.AddSingleton(_mockGetByName.Object);
                services.AddSingleton(_mockAddEmployees.Object);
            });
        });
        _client = factoryWithMock.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task GetEmployees_CallsHandlerWithCorrectPagination()
    {
        var employees = new List<Employee>
        {
            new() { Name = "김테스트", Email = "test@test.com", Phone = "01012345678", Joined = new DateTime(2020, 1, 1) }
        };
        _mockGetEmployees.Setup(h => h.Handle(It.Is<GetEmployeesQuery>(q => q.Page == 2 && q.PageSize == 5)))
            .Returns(new GetEmployeesResult(employees.AsReadOnly(), 11));

        var response = await _client.GetAsync("/api/employee?page=2&pageSize=5");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _mockGetEmployees.Verify(h => h.Handle(It.Is<GetEmployeesQuery>(q => q.Page == 2 && q.PageSize == 5)), Times.Once);
    }

    [Fact]
    public async Task GetEmployeeByName_ReturnsNotFound_WhenHandlerReturnsNull()
    {
        _mockGetByName.Setup(h => h.Handle(It.Is<GetEmployeeByNameQuery>(q => q.Name == "없는사람")))
            .Returns((Employee?)null);

        var response = await _client.GetAsync("/api/employee/없는사람");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        _mockGetByName.Verify(h => h.Handle(It.Is<GetEmployeeByNameQuery>(q => q.Name == "없는사람")), Times.Once);
    }

    [Fact]
    public async Task GetEmployeeByName_ReturnsOk_WhenHandlerReturnsEmployee()
    {
        var employee = new Employee
        {
            Name = "박모크",
            Email = "mock@test.com",
            Phone = "01099998888",
            Joined = new DateTime(2022, 6, 15)
        };
        _mockGetByName.Setup(h => h.Handle(It.Is<GetEmployeeByNameQuery>(q => q.Name == "박모크")))
            .Returns(employee);

        var response = await _client.GetAsync("/api/employee/박모크");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(), _json);
        Assert.Equal("박모크", body.GetProperty("name").GetString());
        Assert.Equal("mock@test.com", body.GetProperty("email").GetString());
        _mockGetByName.Verify(h => h.Handle(It.Is<GetEmployeeByNameQuery>(q => q.Name == "박모크")), Times.Once);
    }

    [Fact]
    public async Task PostCsv_CallsCommandHandler()
    {
        var parsed = new List<Employee>
        {
            new() { Name = "김파싱", Email = "parse@test.com", Phone = "01011112222", Joined = new DateTime(2020, 5, 1) }
        };
        _mockAddEmployees.Setup(h => h.Handle(It.IsAny<AddEmployeesCommand>()))
            .Returns(parsed);

        var csv = "김파싱, parse@test.com 01011112222, 2020.05.01";
        var content = new StringContent(csv, Encoding.UTF8, "text/csv");

        var response = await _client.PostAsync("/api/employee", content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        _mockAddEmployees.Verify(h => h.Handle(It.IsAny<AddEmployeesCommand>()), Times.Once);
    }
}
