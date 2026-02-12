namespace CompanyC.Api.IntegrationTests;

public sealed class EmployeeApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly HttpClient _client = factory.CreateClient();
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };
    private readonly List<WebApplicationFactory<Program>> _factories = [];

    public void Dispose()
    {
        foreach (var f in _factories)
            f.Dispose();
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

    // === POST /api/employee - 과제 문서의 실제 CSV 형식 (email과 phone이 공백 구분) ===

    [Fact]
    public async Task PostCsv_Body_WithSpaceSeparatedFields_ReturnsCreated()
    {
        var client = CreateIsolatedClient();
        var csv = "김철수, charles@clovf.com 01075312468, 2018.03.07\n박영희, matilda@clovf.com 01087654321, , 2021.04.28\n홍길동, kildong-hong@clovf.com 01012345678, 2015.08.15";
        var content = new StringContent(csv, Encoding.UTF8, "text/csv");

        var response = await client.PostAsync("/api/employee", content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await Deserialize<CreatedResponse>(response);
        Assert.Equal(3, body.Count);
        Assert.Equal("charles@clovf.com", body.Data[0].Email);
        Assert.Equal("01075312468", body.Data[0].Phone);
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
        _factories.Add(factory);
        return factory.CreateClient();
    }

    private static async Task PostCsvBody(HttpClient client, string csv)
    {
        var content = new StringContent(csv, Encoding.UTF8, "text/csv");
        await client.PostAsync("/api/employee", content);
    }

    private static async Task<T> Deserialize<T>(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(json, _json)!;
    }

    private sealed record PagedResponse(int Page, int PageSize, int TotalCount, int TotalPages, EmployeeDto[] Data);

    private sealed record CreatedResponse(int Count, EmployeeDto[] Data);

    private sealed record EmployeeDto(string Name, string Email, string Phone, DateTime JoinedDate);
}
