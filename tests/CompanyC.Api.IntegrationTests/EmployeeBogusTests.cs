using CompanyC.Api;

namespace CompanyC.Api.IntegrationTests;

public sealed class EmployeeBogusTests : IDisposable
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };
    private readonly List<WebApplicationFactory<Program>> _factories = [];

    public void Dispose()
    {
        foreach (var f in _factories)
            f.Dispose();
    }

    private HttpClient CreateIsolatedClient()
    {
        var factory = new WebApplicationFactory<Program>();
        _factories.Add(factory);
        return factory.CreateClient();
    }

    [Fact]
    public async Task PostCsv_WithBogusData_ReturnsAllEmployees()
    {
        var client = CreateIsolatedClient();
        var count = 20;
        var csv = EmployeeFaker.GenerateCsv(count);
        var content = new StringContent(csv, Encoding.UTF8, "text/csv");

        var postResponse = await client.PostAsync("/api/employee", content);
        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);

        var getResponse = await client.GetAsync($"/api/employee?page=1&pageSize={count + 10}");
        var body = JsonSerializer.Deserialize<JsonElement>(
            await getResponse.Content.ReadAsStringAsync(), _json);

        Assert.Equal(count, body.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task PostJson_WithBogusData_ReturnsAllEmployees()
    {
        var client = CreateIsolatedClient();
        var count = 15;
        var json = EmployeeFaker.GenerateJson(count);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var postResponse = await client.PostAsync("/api/employee", content);
        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);

        var getResponse = await client.GetAsync($"/api/employee?page=1&pageSize={count + 10}");
        var body = JsonSerializer.Deserialize<JsonElement>(
            await getResponse.Content.ReadAsStringAsync(), _json);

        Assert.Equal(count, body.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task GetByName_WithBogusData_FindsCorrectEmployee()
    {
        var client = CreateIsolatedClient();
        var employees = EmployeeFaker.Generate(10);

        // Register via CSV format
        var sb = new StringBuilder();
        foreach (var e in employees)
            sb.AppendLine($"{e.Name}, {e.Email} {e.Phone}, {e.JoinedDate:yyyy.MM.dd}");
        var content = new StringContent(sb.ToString(), Encoding.UTF8, "text/csv");
        await client.PostAsync("/api/employee", content);

        // Search for first employee by name
        var target = employees[0];
        var response = await client.GetAsync($"/api/employee/{Uri.EscapeDataString(target.Name)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonSerializer.Deserialize<JsonElement>(
            await response.Content.ReadAsStringAsync(), _json);
        Assert.Equal(target.Name, body.GetProperty("name").GetString());
        Assert.Equal(target.Email, body.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Pagination_WithBogusData_ReturnsCorrectPages()
    {
        var client = CreateIsolatedClient();
        var count = 25;
        var csv = EmployeeFaker.GenerateCsv(count);
        var content = new StringContent(csv, Encoding.UTF8, "text/csv");
        await client.PostAsync("/api/employee", content);

        // Page 1 of size 10
        var page1 = await client.GetAsync("/api/employee?page=1&pageSize=10");
        var body1 = JsonSerializer.Deserialize<JsonElement>(
            await page1.Content.ReadAsStringAsync(), _json);
        Assert.Equal(25, body1.GetProperty("totalCount").GetInt32());
        Assert.Equal(3, body1.GetProperty("totalPages").GetInt32());
        Assert.Equal(10, body1.GetProperty("data").GetArrayLength());

        // Page 3 of size 10 should have 5 items
        var page3 = await client.GetAsync("/api/employee?page=3&pageSize=10");
        var body3 = JsonSerializer.Deserialize<JsonElement>(
            await page3.Content.ReadAsStringAsync(), _json);
        Assert.Equal(5, body3.GetProperty("data").GetArrayLength());
    }

    [Fact]
    public async Task ParseCsv_RoundTrip_PreservesData()
    {
        var client = CreateIsolatedClient();
        var employees = EmployeeFaker.Generate(5);

        // Build CSV and post
        var sb = new StringBuilder();
        foreach (var e in employees)
            sb.AppendLine($"{e.Name}, {e.Email} {e.Phone}, {e.JoinedDate:yyyy.MM.dd}");
        var content = new StringContent(sb.ToString(), Encoding.UTF8, "text/csv");
        await client.PostAsync("/api/employee", content);

        // Verify each employee
        foreach (var expected in employees)
        {
            var response = await client.GetAsync($"/api/employee/{Uri.EscapeDataString(expected.Name)}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = JsonSerializer.Deserialize<JsonElement>(
                await response.Content.ReadAsStringAsync(), _json);
            Assert.Equal(expected.Email, body.GetProperty("email").GetString());
            Assert.Equal(expected.Phone, body.GetProperty("phone").GetString());
        }
    }

    [Fact]
    public async Task ParseJson_RoundTrip_PreservesData()
    {
        var client = CreateIsolatedClient();
        var employees = EmployeeFaker.Generate(5);

        // Build JSON and post
        var jsonArray = employees.Select(e => new
        {
            name = e.Name,
            email = e.Email,
            tel = e.Phone,
            joined = e.JoinedDate.ToString("yyyy-MM-dd")
        }).ToList();
        var jsonStr = JsonSerializer.Serialize(jsonArray);
        var content = new StringContent(jsonStr, Encoding.UTF8, "application/json");
        await client.PostAsync("/api/employee", content);

        // Verify each employee
        foreach (var expected in employees)
        {
            var response = await client.GetAsync($"/api/employee/{Uri.EscapeDataString(expected.Name)}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = JsonSerializer.Deserialize<JsonElement>(
                await response.Content.ReadAsStringAsync(), _json);
            Assert.Equal(expected.Email, body.GetProperty("email").GetString());
            Assert.Equal(expected.Phone, body.GetProperty("phone").GetString());
        }
    }
}
