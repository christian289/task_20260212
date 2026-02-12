using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CompanyC.Api.IntegrationTests;

public sealed class EmployeeApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public EmployeeApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    // === GET /api/employee (목록 조회) ===

    [Fact]
    public async Task GetEmployees_ReturnsOk_WithEmptyList()
    {
        var response = await _client.GetAsync("/api/employee?page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await Deserialize<PagedResponse>(response);
        Assert.Equal(0, body.TotalCount);
    }

    [Fact]
    public async Task GetEmployees_ReturnsPaginatedResults()
    {
        var client = CreateIsolatedClient();

        // 3명 등록
        await PostCsvBody(client, "김철수, charles@clovf.com, 01075312468, 2018.03.07\n박영희, matilda@clovf.com, 01087654321, 2021.04.28\n홍길동, kildong-hong@clovf.com, 01012345678, 2015.08.15");

        // page=1, pageSize=2
        var response = await client.GetAsync("/api/employee?page=1&pageSize=2");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await Deserialize<PagedResponse>(response);
        Assert.Equal(3, body.TotalCount);
        Assert.Equal(2, body.TotalPages);
        Assert.Equal(2, body.Data.Length);
    }

    // === GET /api/employee/{name} (이름으로 조회) ===

    [Fact]
    public async Task GetEmployeeByName_ReturnsNotFound_WhenNotExists()
    {
        var response = await _client.GetAsync("/api/employee/없는사람");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetEmployeeByName_ReturnsOk_WhenExists()
    {
        var client = CreateIsolatedClient();
        await PostCsvBody(client, "홍길동, kildong-hong@clovf.com, 01012345678, 2015.08.15");

        var response = await client.GetAsync("/api/employee/홍길동");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await Deserialize<EmployeeDto>(response);
        Assert.Equal("홍길동", body.Name);
        Assert.Equal("kildong-hong@clovf.com", body.Email);
    }

    // === POST /api/employee - CSV body 직접 입력 ===

    [Fact]
    public async Task PostCsv_Body_ReturnsCreated()
    {
        var client = CreateIsolatedClient();
        var csv = "김철수, charles@clovf.com, 01075312468, 2018.03.07";
        var content = new StringContent(csv, Encoding.UTF8, "text/csv");

        var response = await client.PostAsync("/api/employee", content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // === POST /api/employee - JSON body 직접 입력 ===

    [Fact]
    public async Task PostJson_Body_ReturnsCreated()
    {
        var client = CreateIsolatedClient();
        var json = """
        [
            {"name":"김클로","email":"clo@clovf.com","tel":"010-1111-2424","joined":"2012-01-05"}
        ]
        """;
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/employee", content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // === POST /api/employee - CSV 파일 업로드 ===

    [Fact]
    public async Task PostCsv_FileUpload_ReturnsCreated()
    {
        var client = CreateIsolatedClient();
        var csv = "김철수, charles@clovf.com, 01075312468, 2018.03.07\n박영희, matilda@clovf.com, 01087654321, 2021.04.28";

        using var formContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        formContent.Add(fileContent, "file", "employees.csv");

        var response = await client.PostAsync("/api/employee", formContent);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await Deserialize<CreatedResponse>(response);
        Assert.Equal(2, body.Count);
    }

    // === POST /api/employee - JSON 파일 업로드 ===

    [Fact]
    public async Task PostJson_FileUpload_ReturnsCreated()
    {
        var client = CreateIsolatedClient();
        var json = """
        [
            {"name":"김클로","email":"clo@clovf.com","tel":"010-1111-2424","joined":"2012-01-05"},
            {"name":"박마블","email":"md@clovf.com","tel":"010-3535-7979","joined":"2013-07-01"}
        ]
        """;

        using var formContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(json));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        formContent.Add(fileContent, "file", "employees.json");

        var response = await client.PostAsync("/api/employee", formContent);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await Deserialize<CreatedResponse>(response);
        Assert.Equal(2, body.Count);
    }

    // === POST /api/employee - 빈 요청 ===

    [Fact]
    public async Task Post_EmptyBody_ReturnsBadRequest()
    {
        var content = new StringContent("", Encoding.UTF8, "text/plain");
        var response = await _client.PostAsync("/api/employee", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // === Helpers ===

    private HttpClient CreateIsolatedClient()
    {
        var factory = new WebApplicationFactory<Program>();
        return factory.CreateClient();
    }

    private async Task PostCsvBody(HttpClient client, string csv)
    {
        var content = new StringContent(csv, Encoding.UTF8, "text/csv");
        await client.PostAsync("/api/employee", content);
    }

    private async Task<T> Deserialize<T>(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(json, _json)!;
    }

    private sealed class PagedResponse
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public EmployeeDto[] Data { get; set; } = [];
    }

    private sealed class CreatedResponse
    {
        public int Count { get; set; }
        public EmployeeDto[] Data { get; set; } = [];
    }

    private sealed class EmployeeDto
    {
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public DateTime JoinedDate { get; set; }
    }
}
