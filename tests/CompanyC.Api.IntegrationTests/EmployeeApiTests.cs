namespace CompanyC.Api.IntegrationTests;

public sealed class EmployeeApiTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>, IDisposable
{
    private readonly HttpClient _client = factory.CreateClient();
    private readonly List<TestWebApplicationFactory> _factories = [];

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

    // === POST /api/employee - 과제 문서의 실제 CSV 형식 (email과 tel이 공백 구분) ===

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
        Assert.Equal("01075312468", body.Data[0].Tel);
    }

    // === POST /api/employee - SQL Injection 방지 ===

    [Fact]
    public async Task PostJson_WithSqlInjectionColumnName_IgnoresMaliciousKey()
    {
        var client = CreateIsolatedClient();
        var json = """
        [
            {"name":"김보안","email":"secure@test.com","tel":"01099999999","joined":"2024-01-01","x'); DROP TABLE Employees;--":"악성값"}
        ]
        """;
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/employee", content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // 정상 데이터는 저장되어야 함
        var getResponse = await client.GetAsync("/api/employee/김보안");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Fact]
    public async Task PostCsv_WithSqlInjectionHeader_IgnoresMaliciousColumn()
    {
        var client = CreateIsolatedClient();
        var csv = "name,email,tel,joined,\"Robert'); DROP TABLE Employees;--\"\n김보안,secure@test.com,01099999999,2024.01.01,악성값";
        var content = new StringContent(csv, Encoding.UTF8, "text/csv");

        var response = await client.PostAsync("/api/employee", content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // 테이블이 여전히 존재하고 데이터가 조회됨
        var getResponse = await client.GetAsync("/api/employee/김보안");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    // === POST /api/employee - 빈 요청 ===

    [Fact]
    public async Task Post_EmptyBody_ReturnsBadRequest()
    {
        var content = new StringContent("", Encoding.UTF8, "text/plain");
        var response = await _client.PostAsync("/api/employee", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_MultipartWithNoFile_ReturnsBadRequest()
    {
        var client = CreateIsolatedClient();
        using var formContent = new MultipartFormDataContent();

        var response = await client.PostAsync("/api/employee", formContent);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostJson_InvalidJson_ReturnsError()
    {
        var content = new StringContent("{ invalid json }", Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/api/employee", content);

        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task PostJson_InvalidEmail_ReturnsBadRequest()
    {
        var client = CreateIsolatedClient();
        var json = """[{"name":"테스트","email":"not-an-email","tel":"01012345678","joined":"2024-01-01"}]""";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/employee", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostJson_InvalidTel_ReturnsBadRequest()
    {
        var client = CreateIsolatedClient();
        var json = """[{"name":"테스트","email":"test@test.com","tel":"12345","joined":"2024-01-01"}]""";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/employee", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // === PUT /api/employee/{name} (직원 수정) ===

    [Fact]
    public async Task PutEmployee_ReturnsOk_WhenUpdated()
    {
        var client = CreateIsolatedClient();
        await PostCsvBody(client, "홍길동, kildong-hong@clovf.com, 01012345678, 2015.08.15");

        var updateJson = """{"email":"new-email@clovf.com"}""";
        var response = await client.PutAsync("/api/employee/홍길동",
            new StringContent(updateJson, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await Deserialize<EmployeeDto>(response);
        Assert.Equal("홍길동", body.Name);
        Assert.Equal("new-email@clovf.com", body.Email);
        Assert.Equal("01012345678", body.Tel);
    }

    [Fact]
    public async Task PutEmployee_ReturnsNotFound_WhenNotExists()
    {
        var client = CreateIsolatedClient();
        var updateJson = """{"email":"test@test.com"}""";
        var response = await client.PutAsync("/api/employee/없는사람",
            new StringContent(updateJson, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PutEmployee_UpdatesMultipleFields()
    {
        var client = CreateIsolatedClient();
        await PostCsvBody(client, "김철수, charles@clovf.com, 01075312468, 2018.03.07");

        var updateJson = """{"email":"updated@clovf.com","tel":"01099998888","joined":"2020-01-01"}""";
        var response = await client.PutAsync("/api/employee/김철수",
            new StringContent(updateJson, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await Deserialize<EmployeeDto>(response);
        Assert.Equal("updated@clovf.com", body.Email);
        Assert.Equal("01099998888", body.Tel);
        Assert.Equal(new DateTime(2020, 1, 1), body.Joined);
    }

    [Fact]
    public async Task PutEmployee_InvalidEmail_ReturnsBadRequest()
    {
        var client = CreateIsolatedClient();
        await PostCsvBody(client, "홍길동, kildong-hong@clovf.com, 01012345678, 2015.08.15");

        var updateJson = """{"email":"not-an-email"}""";
        var response = await client.PutAsync("/api/employee/홍길동",
            new StringContent(updateJson, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutEmployee_UpdatedDataRetrievable()
    {
        var client = CreateIsolatedClient();
        await PostCsvBody(client, "박영희, matilda@clovf.com, 01087654321, 2021.04.28");

        var updateJson = """{"email":"new-matilda@clovf.com"}""";
        await client.PutAsync("/api/employee/박영희",
            new StringContent(updateJson, Encoding.UTF8, "application/json"));

        var getResponse = await client.GetAsync("/api/employee/박영희");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var body = await Deserialize<EmployeeDto>(getResponse);
        Assert.Equal("new-matilda@clovf.com", body.Email);
    }

    [Fact]
    public async Task PutEmployee_DuplicateAfterUpdate_ReturnsConflict()
    {
        var client = CreateIsolatedClient();
        // 두 명 등록
        await PostCsvBody(client, "김철수, charles@clovf.com, 01075312468, 2018.03.07\n박영희, matilda@clovf.com, 01087654321, 2021.04.28");

        // 박영희를 김철수와 동일한 정보로 수정 시도
        var updateJson = """{"name":"김철수","email":"charles@clovf.com","tel":"01075312468","joined":"2018-03-07"}""";
        var response = await client.PutAsync("/api/employee/박영희",
            new StringContent(updateJson, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // === Helpers ===

    private HttpClient CreateIsolatedClient()
    {
        var f = new TestWebApplicationFactory();
        _factories.Add(f);
        return f.CreateClient();
    }

    private static async Task PostCsvBody(HttpClient client, string csv)
    {
        var content = new StringContent(csv, Encoding.UTF8, "text/csv");
        await client.PostAsync("/api/employee", content);
    }

    private static async Task<T> Deserialize<T>(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(json, TestJsonOptions.CaseInsensitive)!;
    }

    private sealed record PagedResponse(int Page, int PageSize, int TotalCount, int TotalPages, EmployeeDto[] Data);

    private sealed record CreatedResponse(int Count, EmployeeDto[] Data);

    private sealed record EmployeeDto(string Name, string Email, string Tel, DateTime Joined);
}
