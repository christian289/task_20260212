using CompanyC.Api;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
builder.Services.AddSingleton<EmployeeService>();
builder.Services.AddSingleton<IEmployeeService>(sp => sp.GetRequiredService<EmployeeService>());

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference();

// GET /api/employee?page={page}&pageSize={pageSize}
app.MapGet("/api/employee", (IEmployeeService svc, int page = 1, int pageSize = 10) =>
{
    if (page < 1) page = 1;
    if (pageSize < 1) pageSize = 10;

    var (items, totalCount) = svc.GetAll(page, pageSize);
    return Results.Ok(new
    {
        page,
        pageSize,
        totalCount,
        totalPages = (int)Math.Ceiling((double)totalCount / pageSize),
        data = items
    });
});

// GET /api/employee/{name}
app.MapGet("/api/employee/{name}", (IEmployeeService svc, string name) =>
{
    var employee = svc.GetByName(name);
    return employee is not null
        ? Results.Ok(employee)
        : Results.NotFound(new { message = $"Employee '{name}' not found." });
});

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
            return Results.BadRequest(new { message = "No file uploaded." });

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
            return Results.BadRequest(new { message = "Request body is empty." });

        if (contentType.Contains("json"))
            added = EmployeeService.ParseJson(body);
        else if (contentType.Contains("csv") || contentType.Contains("text/plain"))
            added = EmployeeService.ParseCsv(body);
        else
            added = InferAndParse(body);
    }

    if (added.Count == 0)
        return Results.BadRequest(new { message = "No valid employee data found." });

    svc.AddFromParsed(added);
    return Results.Created("/api/employee", new { count = added.Count, data = added });
});

app.Run();

static List<Employee> InferAndParse(string content)
{
    var trimmed = content.TrimStart();
    return trimmed.StartsWith('[') || trimmed.StartsWith('{')
        ? EmployeeService.ParseJson(trimmed)
        : EmployeeService.ParseCsv(content);
}