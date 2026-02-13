using CompanyC.Api.Commands;
using CompanyC.Api.Models;
using CompanyC.Api.Parsers;
using CompanyC.Api.Queries;
using CompanyC.Api.Repositories;

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
builder.Services.AddSingleton<IEmployeeParser, CsvEmployeeParser>();
builder.Services.AddSingleton<IEmployeeParser, JsonEmployeeParser>();
builder.Services.AddSingleton<IGetEmployeesQueryHandler, GetEmployeesQueryHandler>();
builder.Services.AddSingleton<IGetEmployeeByNameQueryHandler, GetEmployeeByNameQueryHandler>();
builder.Services.AddSingleton<IAddEmployeesCommandHandler, AddEmployeesCommandHandler>();

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.Title = "CompanyC API";
    options.Theme = ScalarTheme.BluePlanet;
});

// GET /api/employee?page={page}&pageSize={pageSize}
app.MapGet("/api/employee", (IGetEmployeesQueryHandler handler, int page = 1, int pageSize = 10) =>
{
    if (page < 1) page = 1;
    if (pageSize < 1) pageSize = 10;

    var result = handler.Handle(new GetEmployeesQuery(page, pageSize));
    return Results.Ok(new PagedResponse(
        page,
        pageSize,
        result.TotalCount,
        (int)Math.Ceiling((double)result.TotalCount / pageSize),
        result.Items.ToArray()));
})
.WithName("GetEmployees")
.WithTags("Employee")
.WithSummary("직원 목록 조회")
.WithDescription("페이지네이션을 지원하는 직원 목록 조회 API")
.Produces<PagedResponse>();

// GET /api/employee/{name}
app.MapGet("/api/employee/{name}", (IGetEmployeeByNameQueryHandler handler, string name) =>
{
    var employee = handler.Handle(new GetEmployeeByNameQuery(name));
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
app.MapPost("/api/employee", async (HttpRequest request, IAddEmployeesCommandHandler handler) =>
{
    var contentType = request.ContentType?.ToLowerInvariant() ?? "";
    string content;
    string? fileExtension = null;

    // 파일 업로드
    if (contentType.Contains("multipart/form-data") && request.HasFormContentType)
    {
        var form = await request.ReadFormAsync();
        var file = form.Files[0];
        if (file is null || file.Length == 0)
            return Results.BadRequest(new ErrorResponse("No file uploaded."));

        using var reader = new StreamReader(file.OpenReadStream());
        content = await reader.ReadToEndAsync();
        fileExtension = Path.GetExtension(file.FileName)?.ToLowerInvariant();
    }
    // body 직접 입력
    else
    {
        using var reader = new StreamReader(request.Body);
        content = await reader.ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(content))
            return Results.BadRequest(new ErrorResponse("Request body is empty."));
    }

    var added = handler.Handle(new AddEmployeesCommand(content, contentType, fileExtension));

    if (added.Count == 0)
        return Results.BadRequest(new ErrorResponse("No valid employee data found."));

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

// Response DTOs
record PagedResponse(int Page, int PageSize, int TotalCount, int TotalPages, Employee[] Data);
record CreatedResponse(int Count, Employee[] Data);
record ErrorResponse(string Message);
