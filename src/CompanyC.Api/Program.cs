using CompanyC.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, ct) =>
    {
        document.Info = new()
        {
            Title = "CompanyC 직원 긴급 연락망 API",
            Version = "v1",
            Description = "직원 정보를 CSV/JSON으로 등록하고 조회하는 API"
        };
        return Task.CompletedTask;
    });
});
var connectionString = builder.Configuration.GetConnectionString("Default") ?? "Data Source=employees.db";
builder.Services.AddSingleton<IEmployeeRepository>(_ => new SqliteEmployeeRepository(connectionString));
builder.Services.AddSingleton<EmployeeService>();
builder.Services.AddSingleton<IEmployeeService>(sp => sp.GetRequiredService<EmployeeService>());

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.Title = "CompanyC API";
    options.Theme = ScalarTheme.BluePlanet;
});

// GET /api/employee?page={page}&pageSize={pageSize}
app.MapGet("/api/employee", (IEmployeeService svc, int page = 1, int pageSize = 10) =>
{
    if (page < 1) page = 1;
    if (pageSize < 1) pageSize = 10;

    var (items, totalCount) = svc.GetAll(page, pageSize);
    return Results.Ok(new PagedResponse(
        page,
        pageSize,
        totalCount,
        (int)Math.Ceiling((double)totalCount / pageSize),
        items.ToArray()));
})
.WithName("GetEmployees")
.WithTags("Employee")
.WithSummary("직원 목록 조회")
.WithDescription("페이지네이션을 지원하는 직원 목록 조회 API")
.Produces<PagedResponse>();

// GET /api/employee/{name}
app.MapGet("/api/employee/{name}", (IEmployeeService svc, string name) =>
{
    var employee = svc.GetByName(name);
    return employee is not null
        ? Results.Ok(employee)
        : Results.NotFound(new ErrorResponse($"Employee '{name}' not found."));
})
.WithName("GetEmployeeByName")
.WithTags("Employee")
.WithSummary("이름으로 직원 조회")
.WithDescription("직원 이름으로 검색하여 정보를 반환합니다. 없으면 404를 반환합니다.")
.Produces<Employee>()
.Produces<ErrorResponse>(StatusCodes.Status404NotFound);

// POST /api/employee
app.MapPost("/api/employee", async (HttpRequest request, IEmployeeService svc) =>
{
    var contentType = request.ContentType?.ToLowerInvariant() ?? "";
    List<Employee> added;

    // 파일 업로드
    if (contentType.Contains("multipart/form-data") && request.HasFormContentType)
    {
        var form = await request.ReadFormAsync();
        var file = form.Files[0];
        if (file is null || file.Length == 0)
            return Results.BadRequest(new ErrorResponse("No file uploaded."));

        using var reader = new StreamReader(file.OpenReadStream());
        var content = await reader.ReadToEndAsync();
        var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant();

        added = ext switch
        {
            ".json" => EmployeeService.ParseJson(content),
            ".csv" => EmployeeService.ParseCsv(content),
            _ => InferAndParse(content)
        };
    }
    // body 직접 입력
    else
    {
        using var reader = new StreamReader(request.Body);
        var body = await reader.ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(body))
            return Results.BadRequest(new ErrorResponse("Request body is empty."));

        if (contentType.Contains("json"))
            added = EmployeeService.ParseJson(body);
        else if (contentType.Contains("csv") || contentType.Contains("text/plain"))
            added = EmployeeService.ParseCsv(body);
        else
            added = InferAndParse(body);
    }

    if (added.Count == 0)
        return Results.BadRequest(new ErrorResponse("No valid employee data found."));

    svc.AddFromParsed(added);
    return Results.Created("/api/employee", new CreatedResponse(added.Count, added.ToArray()));
})
.WithName("AddEmployees")
.WithTags("Employee")
.WithSummary("직원 추가")
.WithDescription("CSV 또는 JSON 형식으로 직원 정보를 등록합니다. Body 직접 입력(text/csv, application/json) 또는 파일 업로드(multipart/form-data)를 지원합니다.")
.Produces<CreatedResponse>(StatusCodes.Status201Created)
.Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
.DisableAntiforgery();

app.Run();

static List<Employee> InferAndParse(string content)
{
    var trimmed = content.TrimStart();
    return trimmed.StartsWith('[') || trimmed.StartsWith('{')
        ? EmployeeService.ParseJson(trimmed)
        : EmployeeService.ParseCsv(content);
}

// Response DTOs
record PagedResponse(int Page, int PageSize, int TotalCount, int TotalPages, Employee[] Data);
record CreatedResponse(int Count, Employee[] Data);
record ErrorResponse(string Message);
