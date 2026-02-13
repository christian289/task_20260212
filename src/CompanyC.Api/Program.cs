using CompanyC.Api.Commands;
using CompanyC.Api.Errors;
using CompanyC.Api.Models;
using CompanyC.Api.Parsers;
using CompanyC.Api.Queries;
using CompanyC.Api.Repositories;
using CompanyC.Api.Validators;

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
builder.Services.AddValidatorsFromAssemblyContaining<Program>(ServiceLifetime.Singleton);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10MB
});

var app = builder.Build();

app.UseExceptionHandler(appError =>
{
    appError.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(
            new { errors = new[] { new ErrorDetail("Server.UnexpectedError", "예기치 않은 오류가 발생했습니다.") } });
    });
});

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
    if (pageSize > 100) pageSize = 100;

    return handler.Handle(new GetEmployeesQuery(page, pageSize))
        .Match(
            result => Results.Ok(new PagedResponse(
                page, pageSize, result.TotalCount,
                (int)Math.Ceiling((double)result.TotalCount / pageSize),
                result.Items.ToArray())),
            errors => errors.ToProblem());
})
.WithName("GetEmployees")
.WithTags("Employee")
.WithSummary("직원 목록 조회")
.WithDescription("페이지네이션을 지원하는 직원 목록 조회 API")
.Produces<PagedResponse>();

// GET /api/employee/{name}
app.MapGet("/api/employee/{name}", (IGetEmployeeByNameQueryHandler handler, string name) =>
{
    return handler.Handle(new GetEmployeeByNameQuery(name))
        .Match(
            employee => Results.Ok(employee),
            errors => errors.ToProblem());
})
.WithName("GetEmployeeByName")
.WithTags("Employee")
.WithSummary("이름으로 직원 조회")
.WithDescription("직원 이름으로 검색하여 정보를 반환합니다. 없으면 404를 반환합니다.")
.Produces<Employee>()
.ProducesProblem(StatusCodes.Status404NotFound);

// POST /api/employee
app.MapPost("/api/employee", async (HttpRequest request, IAddEmployeesCommandHandler handler) =>
{
    var contentType = request.ContentType?.ToLowerInvariant() ?? "";
    string content;
    string? fileExtension = null;

    if (contentType.Contains("multipart/form-data") && request.HasFormContentType)
    {
        var form = await request.ReadFormAsync();
        var file = form.Files.Count > 0 ? form.Files[0] : null;
        if (file is null || file.Length == 0)
            return EmployeeErrors.NoFileUploaded.ToList().ToProblem();

        using var reader = new StreamReader(file.OpenReadStream());
        content = await reader.ReadToEndAsync();
        fileExtension = Path.GetExtension(file.FileName)?.ToLowerInvariant();
    }
    else
    {
        using var reader = new StreamReader(request.Body);
        content = await reader.ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(content))
            return EmployeeErrors.EmptyBody.ToList().ToProblem();
    }

    return handler.Handle(new AddEmployeesCommand(content, contentType, fileExtension))
        .Match(
            added => Results.Created("/api/employee", new CreatedResponse(added.Count, added.ToArray())),
            errors => errors.ToProblem());
})
.WithName("AddEmployees")
.WithTags("Employee")
.WithSummary("직원 추가")
.WithDescription("CSV 또는 JSON 형식으로 직원 정보를 등록합니다. Body 직접 입력(text/csv, application/json) 또는 파일 업로드(multipart/form-data)를 지원합니다.")
.Produces<CreatedResponse>(StatusCodes.Status201Created)
.ProducesProblem(StatusCodes.Status400BadRequest)
.DisableAntiforgery();

app.Run();

// Response DTOs
record PagedResponse(int Page, int PageSize, int TotalCount, int TotalPages, Employee[] Data);
record CreatedResponse(int Count, Employee[] Data);
record ErrorDetail(string Code, string Description);

static class ErrorOrExtensions
{
    internal static IResult ToProblem(this List<Error> errors)
    {
        var first = errors[0];
        var statusCode = first.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };

        var details = errors.Select(e => new ErrorDetail(e.Code, e.Description)).ToArray();
        return Results.Json(new { errors = details }, statusCode: statusCode);
    }

    internal static List<Error> ToList(this Error error) => [error];
}
