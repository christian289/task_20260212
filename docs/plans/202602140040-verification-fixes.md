# CompanyC API 검증 결과 개선 구현 계획 (v3)

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 과제 조건 대비 검증에서 발견된 미흡 사항을 모두 수정하여 ErrorOr 기반 명시적 오류 처리, FluentValidation 기반 데이터 정합성 검증, 구조적 로깅, 방어적 프로그래밍을 달성한다.

**Architecture:** 기존 Minimal API + CQRS + Repository 아키텍처를 유지하면서:
1. ErrorOr 라이브러리로 CQRS Handler 반환 타입을 전환 (명시적 오류 전파)
2. FluentValidation으로 파싱된 Employee 데이터의 정합성을 검증 (파싱 후 → 저장 전)
3. ILogger를 적절한 로그 레벨 + BeginScope + 구조적 템플릿으로 주입

**Tech Stack:** .NET 10, ASP.NET Core Minimal API, SQLite, ErrorOr, FluentValidation, xUnit, Moq, Bogus, ILogger

**Note:** Task 1(빌드 실패 수정)은 이미 수동 완료됨. Task 번호는 1부터 재시작.

**정규식 분석 결과:**
- `SqliteEmployeeRepository.GeneratedRegex` → SQL 컬럼명 보안 검증 → **유지** (FluentValidation 대상 아님)
- `CsvEmployeeParser.Contains('@')` / `IsTelNumber()` → CSV 파싱 휴리스틱 → **유지** (파싱 감지 로직)
- Employee 데이터 정합성 (Email 형식, Tel 형식, Name 필수) → **FluentValidation 신규 추가**

---

## Task 1: ErrorOr + FluentValidation 패키지 설치 및 도메인 에러/Validator 정의

**Files:**
- Modify: `src/CompanyC.Api/CompanyC.Api.csproj`
- Create: `src/CompanyC.Api/Errors/EmployeeErrors.cs`
- Create: `src/CompanyC.Api/Validators/EmployeeValidator.cs`
- Modify: `src/CompanyC.Api/GlobalUsings.cs`

**Step 1: NuGet 패키지 설치**

```bash
dotnet add src/CompanyC.Api package ErrorOr
dotnet add src/CompanyC.Api package FluentValidation
dotnet add src/CompanyC.Api package FluentValidation.DependencyInjectionExtensions
```

**Step 2: GlobalUsings에 네임스페이스 추가**

`src/CompanyC.Api/GlobalUsings.cs`에 추가:

```csharp
global using ErrorOr;
global using FluentValidation;
```

**Step 3: 도메인 에러 정의 클래스 생성**

`src/CompanyC.Api/Errors/EmployeeErrors.cs`:

```csharp
namespace CompanyC.Api.Errors;

public static class EmployeeErrors
{
    public static Error NotFound(string name) => Error.NotFound(
        code: "Employee.NotFound",
        description: $"'{name}' 이름의 직원을 찾을 수 없습니다.");

    public static readonly Error NoFileUploaded = Error.Validation(
        code: "Employee.NoFileUploaded",
        description: "업로드된 파일이 없거나 파일이 비어 있습니다.");

    public static readonly Error EmptyBody = Error.Validation(
        code: "Employee.EmptyBody",
        description: "요청 본문이 비어 있습니다.");

    public static readonly Error NoParserFound = Error.Failure(
        code: "Employee.NoParserFound",
        description: "지원되지 않는 데이터 형식입니다. CSV 또는 JSON 형식을 사용하세요.");

    public static readonly Error NoValidData = Error.Validation(
        code: "Employee.NoValidData",
        description: "유효한 직원 데이터를 찾을 수 없습니다. 필수 필드(Name, Email, Tel)를 확인하세요.");

    public static Error ParseFailed(string format, string reason) => Error.Failure(
        code: "Employee.ParseFailed",
        description: $"{format} 파싱 실패: {reason}");

    public static Error StorageFailed(string reason) => Error.Unexpected(
        code: "Employee.StorageFailed",
        description: $"데이터 저장 중 오류가 발생했습니다: {reason}");

    public static Error ValidationFailed(string details) => Error.Validation(
        code: "Employee.ValidationFailed",
        description: details);
}
```

**Step 4: Employee Validator 생성**

`src/CompanyC.Api/Validators/EmployeeValidator.cs`:

```csharp
using CompanyC.Api.Models;

namespace CompanyC.Api.Validators;

public sealed class EmployeeValidator : AbstractValidator<Employee>
{
    public EmployeeValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("이름은 필수입니다.")
            .MaximumLength(100)
            .WithMessage("이름은 100자를 초과할 수 없습니다.");

        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("이메일은 필수입니다.")
            .EmailAddress()
            .WithMessage("올바른 이메일 형식이 아닙니다: '{PropertyValue}'");

        RuleFor(x => x.Tel)
            .NotEmpty()
            .WithMessage("전화번호는 필수입니다.")
            .Matches(@"^(01[016789])-?\d{3,4}-?\d{4}$")
            .WithMessage("올바른 전화번호 형식이 아닙니다: '{PropertyValue}' (예: 01012345678, 010-1234-5678)");

        RuleFor(x => x.Joined)
            .Must(d => d != default)
            .WithMessage("입사일이 유효하지 않습니다.")
            .LessThanOrEqualTo(DateTime.Now.Date.AddDays(1))
            .WithMessage("입사일은 미래일 수 없습니다.");
    }
}
```

**Step 5: DI에 Validator 등록 (Program.cs 선행 준비)**

`src/CompanyC.Api/Program.cs`의 DI 영역에 추가:

```csharp
builder.Services.AddValidatorsFromAssemblyContaining<Program>(ServiceLifetime.Singleton);
```

**Step 6: 빌드 확인**

Run: `dotnet build --verbosity quiet`
Expected: 빌드 성공

**Step 7: 커밋**

```bash
git add src/CompanyC.Api/
git commit -m "기능: ErrorOr + FluentValidation 패키지 설치 및 도메인 에러/Validator 정의"
```

---

## Task 2: CQRS Handler를 ErrorOr 반환 타입으로 전환

**Files:**
- Modify: `src/CompanyC.Api/Queries/GetEmployeesQuery.cs`
- Modify: `src/CompanyC.Api/Queries/GetEmployeeByNameQuery.cs`
- Modify: `src/CompanyC.Api/Commands/AddEmployeesCommand.cs`
- Modify: `src/CompanyC.Api/Repositories/IEmployeeRepository.cs`
- Modify: `src/CompanyC.Api/Repositories/SqliteEmployeeRepository.cs`
- Modify: `src/CompanyC.Api/Parsers/IEmployeeParser.cs`
- Modify: `src/CompanyC.Api/Parsers/CsvEmployeeParser.cs`
- Modify: `src/CompanyC.Api/Parsers/JsonEmployeeParser.cs`

**Step 1: GetEmployeeByNameQuery Handler를 ErrorOr로 변경**

`src/CompanyC.Api/Queries/GetEmployeeByNameQuery.cs`:

```csharp
using CompanyC.Api.Errors;
using CompanyC.Api.Models;
using CompanyC.Api.Repositories;

namespace CompanyC.Api.Queries;

public record GetEmployeeByNameQuery(string Name);

public interface IGetEmployeeByNameQueryHandler
{
    ErrorOr<Employee> Handle(GetEmployeeByNameQuery query);
}

public sealed class GetEmployeeByNameQueryHandler(IEmployeeRepository repository) : IGetEmployeeByNameQueryHandler
{
    public ErrorOr<Employee> Handle(GetEmployeeByNameQuery query)
    {
        var employee = repository.GetByName(query.Name);
        return employee is not null
            ? employee
            : EmployeeErrors.NotFound(query.Name);
    }
}
```

**Step 2: GetEmployeesQuery Handler를 ErrorOr로 변경**

`src/CompanyC.Api/Queries/GetEmployeesQuery.cs`:

```csharp
using CompanyC.Api.Models;
using CompanyC.Api.Repositories;

namespace CompanyC.Api.Queries;

public record GetEmployeesQuery(int Page, int PageSize);

public record GetEmployeesResult(IReadOnlyList<Employee> Items, int TotalCount);

public interface IGetEmployeesQueryHandler
{
    ErrorOr<GetEmployeesResult> Handle(GetEmployeesQuery query);
}

public sealed class GetEmployeesQueryHandler(IEmployeeRepository repository) : IGetEmployeesQueryHandler
{
    public ErrorOr<GetEmployeesResult> Handle(GetEmployeesQuery query)
    {
        var (items, totalCount) = repository.GetAll(query.Page, query.PageSize);
        return new GetEmployeesResult(items, totalCount);
    }
}
```

**Step 3: AddEmployeesCommand Handler를 ErrorOr + FluentValidation으로 변경**

핵심: 파싱 후 → FluentValidation 검증 → 유효한 것만 저장 (부분 성공 허용)

`src/CompanyC.Api/Commands/AddEmployeesCommand.cs`:

```csharp
using CompanyC.Api.Errors;
using CompanyC.Api.Models;
using CompanyC.Api.Parsers;
using CompanyC.Api.Repositories;

namespace CompanyC.Api.Commands;

public record AddEmployeesCommand(string Content, string? ContentType, string? FileExtension);

public interface IAddEmployeesCommandHandler
{
    ErrorOr<List<Employee>> Handle(AddEmployeesCommand command);
}

public sealed class AddEmployeesCommandHandler(
    IEmployeeRepository repository,
    IEnumerable<IEmployeeParser> parsers,
    IValidator<Employee> employeeValidator) : IAddEmployeesCommandHandler
{
    public ErrorOr<List<Employee>> Handle(AddEmployeesCommand command)
    {
        var parser = parsers.FirstOrDefault(p => p.CanParse(command.ContentType, command.FileExtension));

        // content sniffing fallback
        if (parser is null)
        {
            var trimmed = command.Content.TrimStart();
            var inferredType = trimmed.StartsWith('[') || trimmed.StartsWith('{')
                ? "application/json"
                : "text/csv";
            parser = parsers.FirstOrDefault(p => p.CanParse(inferredType, null));
        }

        if (parser is null)
            return EmployeeErrors.NoParserFound;

        var parseResult = parser.Parse(command.Content);
        if (parseResult.IsError)
            return parseResult.Errors;

        var parsed = parseResult.Value;
        if (parsed.Count == 0)
            return EmployeeErrors.NoValidData;

        // FluentValidation: 각 Employee 검증
        List<Error> validationErrors = [];
        List<Employee> validEmployees = [];

        for (var i = 0; i < parsed.Count; i++)
        {
            var result = employeeValidator.Validate(parsed[i]);
            if (result.IsValid)
            {
                validEmployees.Add(parsed[i]);
            }
            else
            {
                validationErrors.AddRange(result.Errors.Select(e =>
                    Error.Validation(
                        code: $"Employee[{i}].{e.PropertyName}",
                        description: e.ErrorMessage)));
            }
        }

        // 유효한 직원이 하나도 없으면 전체 검증 에러 반환
        if (validEmployees.Count == 0)
            return validationErrors;

        var storeResult = repository.AddRange(validEmployees);
        if (storeResult.IsError)
            return storeResult.Errors;

        return validEmployees;
    }
}
```

**Step 4: IEmployeeParser.Parse를 ErrorOr로 변경**

`src/CompanyC.Api/Parsers/IEmployeeParser.cs`:

```csharp
using CompanyC.Api.Models;

namespace CompanyC.Api.Parsers;

public interface IEmployeeParser
{
    bool CanParse(string? contentType, string? fileExtension);
    ErrorOr<List<Employee>> Parse(string content);
}
```

**Step 5: CsvEmployeeParser.Parse를 ErrorOr로 변경**

`src/CompanyC.Api/Parsers/CsvEmployeeParser.cs` — `Parse` 메서드 시그니처 변경 + try-catch 래핑:

```csharp
public ErrorOr<List<Employee>> Parse(string content)
{
    try
    {
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        if (lines.Count == 0)
            return new List<Employee>();

        var firstLineParts = lines[0].Split(',').Select(p => p.Trim()).ToArray();
        var hasHeader = firstLineParts.Any(p => KnownHeaders.Contains(p));

        return hasHeader
            ? ParseWithHeaders(lines, firstLineParts)
            : ParseHeuristic(lines);
    }
    catch (Exception ex)
    {
        return EmployeeErrors.ParseFailed("CSV", ex.Message);
    }
}
```

내부 `ParseWithHeaders`와 `ParseHeuristic`은 `List<Employee>` 반환 그대로 유지. `using CompanyC.Api.Errors;` 추가.

**Step 6: JsonEmployeeParser.Parse를 ErrorOr로 변경**

`src/CompanyC.Api/Parsers/JsonEmployeeParser.cs` — `Parse` 메서드:

```csharp
public ErrorOr<List<Employee>> Parse(string content)
{
    try
    {
        // ... 기존 파싱 로직 유지 ...
        return result;
    }
    catch (JsonException ex)
    {
        return EmployeeErrors.ParseFailed("JSON", ex.Message);
    }
    catch (Exception ex)
    {
        return EmployeeErrors.ParseFailed("JSON", ex.Message);
    }
}
```

`using CompanyC.Api.Errors;` 추가.

**Step 7: IEmployeeRepository.AddRange를 ErrorOr로 변경**

`src/CompanyC.Api/Repositories/IEmployeeRepository.cs`:

```csharp
using CompanyC.Api.Models;

namespace CompanyC.Api.Repositories;

public interface IEmployeeRepository
{
    (IReadOnlyList<Employee> Items, int TotalCount) GetAll(int page, int pageSize);
    Employee? GetByName(string name);
    ErrorOr<Success> AddRange(List<Employee> employees);
}
```

`src/CompanyC.Api/Repositories/SqliteEmployeeRepository.cs` — `AddRange` 메서드:

```csharp
public ErrorOr<Success> AddRange(List<Employee> employees)
{
    if (employees.Count == 0)
        return Result.Success;

    try
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        // ... 기존 EnsureColumns, INSERT, transaction 로직 유지 ...

        return Result.Success;
    }
    catch (Exception ex)
    {
        return EmployeeErrors.StorageFailed(ex.Message);
    }
}
```

`using CompanyC.Api.Errors;` 추가.

**Step 8: 빌드 확인**

Run: `dotnet build --verbosity quiet`
Expected: 빌드 성공 (테스트 프로젝트에서 ErrorOr 관련 오류 발생 가능 — Task 5에서 수정)

**Step 9: 커밋**

```bash
git add src/CompanyC.Api/
git commit -m "리팩토링: CQRS Handler/Parser/Repository를 ErrorOr 반환 + FluentValidation 검증으로 전환"
```

---

## Task 3: Endpoint를 ErrorOr Match 패턴으로 전환 + 안전한 파일 접근

**Files:**
- Modify: `src/CompanyC.Api/Program.cs`
- Modify: `tests/CompanyC.Api.IntegrationTests/EmployeeApiTests.cs`

**Step 1: 신규 테스트 작성**

`tests/CompanyC.Api.IntegrationTests/EmployeeApiTests.cs`에 추가:

```csharp
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
```

**Step 2: Program.cs 전면 개편**

Response DTO 변경:

```csharp
// Response DTOs
record PagedResponse(int Page, int PageSize, int TotalCount, int TotalPages, Employee[] Data);
record CreatedResponse(int Count, Employee[] Data);
record ErrorDetail(string Code, string Description);
```

ErrorOr → IResult 변환 헬퍼 (Program.cs 하단):

```csharp
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
```

DI 영역 변경:

```csharp
builder.Services.AddValidatorsFromAssemblyContaining<Program>(ServiceLifetime.Singleton);
builder.Services.AddSingleton<IEmployeeRepository>(sp =>
    new SqliteEmployeeRepository(connectionString));
// Parser, Handler 등은 기존과 동일 (ILogger는 Task 4에서 추가)
```

GET /api/employee:

```csharp
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
// .WithName, .WithTags 등 기존 메타데이터 유지
```

GET /api/employee/{name}:

```csharp
app.MapGet("/api/employee/{name}", (IGetEmployeeByNameQueryHandler handler, string name) =>
{
    return handler.Handle(new GetEmployeeByNameQuery(name))
        .Match(
            employee => Results.Ok(employee),
            errors => errors.ToProblem());
})
```

POST /api/employee — 안전한 파일 접근:

```csharp
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
```

전역 예외 핸들러 (`app.MapOpenApi()` 앞에):

```csharp
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
```

Kestrel 요청 크기 제한 (`builder.Build()` 전에):

```csharp
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10MB
});
```

**Step 3: 빌드 및 테스트 확인**

Run: `dotnet build --verbosity quiet && dotnet test --verbosity quiet`
Expected: src 빌드 성공. 테스트에서 Mock 관련 컴파일 오류 가능 (Task 5에서 수정). 통합 테스트는 통과.

**Step 4: 커밋**

```bash
git add src/CompanyC.Api/Program.cs tests/CompanyC.Api.IntegrationTests/EmployeeApiTests.cs
git commit -m "리팩토링: Endpoint를 ErrorOr Match 패턴으로 전환 + 전역 예외 핸들러 + FluentValidation 검증 테스트"
```

---

## Task 4: 구조적 로깅 구현 (BeginScope + 적절한 로그 레벨 + 템플릿)

**Files:**
- Modify: `src/CompanyC.Api/Program.cs`
- Modify: `src/CompanyC.Api/Commands/AddEmployeesCommand.cs`
- Modify: `src/CompanyC.Api/Queries/GetEmployeesQuery.cs`
- Modify: `src/CompanyC.Api/Queries/GetEmployeeByNameQuery.cs`
- Modify: `src/CompanyC.Api/Repositories/SqliteEmployeeRepository.cs`
- Modify: `src/CompanyC.Api/Parsers/CsvEmployeeParser.cs`
- Modify: `src/CompanyC.Api/Parsers/JsonEmployeeParser.cs`

### 로그 레벨 가이드

| 레벨 | 용도 | 예시 |
|------|------|------|
| `LogDebug` | 내부 처리 상세 (개발/디버깅용) | 파서 선택, SQL 쿼리 실행, 컬럼 감지, 검증 상세 |
| `LogInformation` | 비즈니스 오퍼레이션 완료 | 직원 N명 등록 완료, 조회 완료, DB 초기화 |
| `LogWarning` | 복구 가능한 문제 | 잘못된 컬럼명 무시, 검증 실패 건수, 빈 요청, 파서 매칭 실패 |
| `LogError` | 실패, 예외 발생 | DB 연결 실패, 파싱 예외, 저장 실패 |

### 구조적 로깅 규칙

- **Template 형태 사용**: `logger.LogInformation("직원 {Count}명 조회", count)` (O)
- **문자열 보간 금지**: `logger.LogInformation($"직원 {count}명 조회")` (X)
- **PascalCase placeholder**: `{Count}`, `{Name}`, `{ContentType}`

**Step 1: Program.cs — Endpoint에 BeginScope + 로깅 적용**

```csharp
// GET /api/employee
app.MapGet("/api/employee", (IGetEmployeesQueryHandler handler, ILogger<Program> logger, int page = 1, int pageSize = 10) =>
{
    using (logger.BeginScope(new Dictionary<string, object>
    {
        ["Endpoint"] = "GET /api/employee",
        ["Page"] = page,
        ["PageSize"] = pageSize
    }))
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        logger.LogDebug("직원 목록 조회 시작: Page={Page}, PageSize={PageSize}", page, pageSize);

        return handler.Handle(new GetEmployeesQuery(page, pageSize))
            .Match(
                result =>
                {
                    logger.LogInformation("직원 목록 조회 완료: {ReturnedCount}/{TotalCount}건",
                        result.Items.Count, result.TotalCount);
                    return Results.Ok(new PagedResponse(
                        page, pageSize, result.TotalCount,
                        (int)Math.Ceiling((double)result.TotalCount / pageSize),
                        result.Items.ToArray()));
                },
                errors =>
                {
                    logger.LogWarning("직원 목록 조회 실패: {ErrorCode} - {ErrorDescription}",
                        errors[0].Code, errors[0].Description);
                    return errors.ToProblem();
                });
    }
})
```

```csharp
// GET /api/employee/{name}
app.MapGet("/api/employee/{name}", (IGetEmployeeByNameQueryHandler handler, ILogger<Program> logger, string name) =>
{
    using (logger.BeginScope(new Dictionary<string, object>
    {
        ["Endpoint"] = "GET /api/employee/{name}",
        ["SearchName"] = name
    }))
    {
        logger.LogDebug("이름으로 직원 조회 시작: Name={Name}", name);

        return handler.Handle(new GetEmployeeByNameQuery(name))
            .Match(
                employee =>
                {
                    logger.LogInformation("직원 조회 성공: {EmployeeName}", employee.Name);
                    return Results.Ok(employee);
                },
                errors =>
                {
                    logger.LogWarning("직원 조회 실패: {ErrorCode} - {ErrorDescription}",
                        errors[0].Code, errors[0].Description);
                    return errors.ToProblem();
                });
    }
})
```

```csharp
// POST /api/employee
app.MapPost("/api/employee", async (HttpRequest request, IAddEmployeesCommandHandler handler, ILogger<Program> logger) =>
{
    var contentType = request.ContentType?.ToLowerInvariant() ?? "";

    using (logger.BeginScope(new Dictionary<string, object>
    {
        ["Endpoint"] = "POST /api/employee",
        ["ContentType"] = contentType
    }))
    {
        string content;
        string? fileExtension = null;

        if (contentType.Contains("multipart/form-data") && request.HasFormContentType)
        {
            var form = await request.ReadFormAsync();
            var file = form.Files.Count > 0 ? form.Files[0] : null;
            if (file is null || file.Length == 0)
            {
                logger.LogWarning("파일 업로드 실패: 파일 없음 또는 빈 파일");
                return EmployeeErrors.NoFileUploaded.ToList().ToProblem();
            }

            fileExtension = Path.GetExtension(file.FileName)?.ToLowerInvariant();
            logger.LogDebug("파일 업로드 수신: FileName={FileName}, Size={FileSize}, Extension={Extension}",
                file.FileName, file.Length, fileExtension);

            using var reader = new StreamReader(file.OpenReadStream());
            content = await reader.ReadToEndAsync();
        }
        else
        {
            using var reader = new StreamReader(request.Body);
            content = await reader.ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(content))
            {
                logger.LogWarning("빈 요청 본문 수신");
                return EmployeeErrors.EmptyBody.ToList().ToProblem();
            }
            logger.LogDebug("본문 직접 입력 수신: ContentLength={ContentLength}", content.Length);
        }

        return handler.Handle(new AddEmployeesCommand(content, contentType, fileExtension))
            .Match(
                added =>
                {
                    logger.LogInformation("직원 {Count}명 등록 완료", added.Count);
                    return Results.Created("/api/employee", new CreatedResponse(added.Count, added.ToArray()));
                },
                errors =>
                {
                    logger.LogWarning("직원 등록 실패: {ErrorCode} - {ErrorDescription}",
                        errors[0].Code, errors[0].Description);
                    return errors.ToProblem();
                });
    }
})
```

**Step 2: CQRS Handler에 ILogger 주입**

`AddEmployeesCommandHandler`:

```csharp
public sealed class AddEmployeesCommandHandler(
    IEmployeeRepository repository,
    IEnumerable<IEmployeeParser> parsers,
    IValidator<Employee> employeeValidator,
    ILogger<AddEmployeesCommandHandler> logger) : IAddEmployeesCommandHandler
{
    public ErrorOr<List<Employee>> Handle(AddEmployeesCommand command)
    {
        var parser = parsers.FirstOrDefault(p => p.CanParse(command.ContentType, command.FileExtension));

        if (parser is null)
        {
            var trimmed = command.Content.TrimStart();
            var inferredType = trimmed.StartsWith('[') || trimmed.StartsWith('{')
                ? "application/json" : "text/csv";
            logger.LogDebug("명시적 파서 매칭 실패, Content Sniffing 시도: InferredType={InferredType}", inferredType);
            parser = parsers.FirstOrDefault(p => p.CanParse(inferredType, null));
        }

        if (parser is null)
        {
            logger.LogWarning("파서를 찾을 수 없음: ContentType={ContentType}, FileExtension={FileExtension}",
                command.ContentType, command.FileExtension);
            return EmployeeErrors.NoParserFound;
        }

        logger.LogDebug("파서 선택: {ParserType}", parser.GetType().Name);

        var parseResult = parser.Parse(command.Content);
        if (parseResult.IsError)
        {
            logger.LogError("파싱 실패: {ErrorCode} - {ErrorDescription}",
                parseResult.FirstError.Code, parseResult.FirstError.Description);
            return parseResult.Errors;
        }

        var parsed = parseResult.Value;
        if (parsed.Count == 0)
        {
            logger.LogWarning("파싱 결과 유효한 직원 데이터 없음");
            return EmployeeErrors.NoValidData;
        }

        // FluentValidation
        List<Error> validationErrors = [];
        List<Employee> validEmployees = [];

        for (var i = 0; i < parsed.Count; i++)
        {
            var result = employeeValidator.Validate(parsed[i]);
            if (result.IsValid)
            {
                validEmployees.Add(parsed[i]);
            }
            else
            {
                foreach (var e in result.Errors)
                {
                    logger.LogDebug("검증 실패 [{Index}]: {Property} - {Message}", i, e.PropertyName, e.ErrorMessage);
                }
                validationErrors.AddRange(result.Errors.Select(e =>
                    Error.Validation(
                        code: $"Employee[{i}].{e.PropertyName}",
                        description: e.ErrorMessage)));
            }
        }

        if (validEmployees.Count == 0)
        {
            logger.LogWarning("전체 {Total}건 중 유효한 데이터 없음, 검증 에러 {ErrorCount}건",
                parsed.Count, validationErrors.Count);
            return validationErrors;
        }

        if (validationErrors.Count > 0)
        {
            logger.LogWarning("부분 검증 성공: {ValidCount}/{TotalCount}건 유효, {ErrorCount}건 스킵",
                validEmployees.Count, parsed.Count, validationErrors.Count);
        }

        var storeResult = repository.AddRange(validEmployees);
        if (storeResult.IsError)
        {
            logger.LogError("저장 실패: {ErrorCode} - {ErrorDescription}",
                storeResult.FirstError.Code, storeResult.FirstError.Description);
            return storeResult.Errors;
        }

        logger.LogInformation("직원 데이터 처리 완료: {Count}명 파싱/검증/저장 성공", validEmployees.Count);
        return validEmployees;
    }
}
```

`GetEmployeesQueryHandler`:

```csharp
public sealed class GetEmployeesQueryHandler(
    IEmployeeRepository repository,
    ILogger<GetEmployeesQueryHandler> logger) : IGetEmployeesQueryHandler
{
    public ErrorOr<GetEmployeesResult> Handle(GetEmployeesQuery query)
    {
        logger.LogDebug("DB 조회 실행: Page={Page}, PageSize={PageSize}", query.Page, query.PageSize);
        var (items, totalCount) = repository.GetAll(query.Page, query.PageSize);
        logger.LogDebug("DB 조회 완료: {ReturnedCount}/{TotalCount}건", items.Count, totalCount);
        return new GetEmployeesResult(items, totalCount);
    }
}
```

`GetEmployeeByNameQueryHandler`:

```csharp
public sealed class GetEmployeeByNameQueryHandler(
    IEmployeeRepository repository,
    ILogger<GetEmployeeByNameQueryHandler> logger) : IGetEmployeeByNameQueryHandler
{
    public ErrorOr<Employee> Handle(GetEmployeeByNameQuery query)
    {
        logger.LogDebug("이름 검색 실행: Name={Name}", query.Name);
        var employee = repository.GetByName(query.Name);
        if (employee is null)
        {
            logger.LogDebug("직원 미발견: Name={Name}", query.Name);
            return EmployeeErrors.NotFound(query.Name);
        }
        return employee;
    }
}
```

**Step 3: Repository에 ILogger 주입**

`SqliteEmployeeRepository` constructor:

```csharp
public SqliteEmployeeRepository(string connectionString, ILogger<SqliteEmployeeRepository> logger)
{
    _connectionString = connectionString;
    _logger = logger;
    InitializeDatabase();
}

private readonly ILogger<SqliteEmployeeRepository> _logger;
```

로깅 포인트:
- `InitializeDatabase()`: `_logger.LogInformation("SQLite 데이터베이스 초기화 완료")`
- `EnsureColumns` 새 컬럼: `_logger.LogInformation("동적 컬럼 추가: {ColumnName}", col)`
- `EnsureColumns` 잘못된 컬럼: `_logger.LogWarning("유효하지 않은 컬럼명 무시: {ColumnName}", col)`
- `AddRange` INSERT: `_logger.LogDebug("{Count}건 INSERT 완료", employees.Count)`
- `AddRange` catch: `_logger.LogError(ex, "직원 데이터 저장 중 오류 발생")`

DI 변경:

```csharp
builder.Services.AddSingleton<IEmployeeRepository>(sp =>
    new SqliteEmployeeRepository(
        connectionString,
        sp.GetRequiredService<ILogger<SqliteEmployeeRepository>>()));
```

**Step 4: Parser에 ILogger 주입**

`CsvEmployeeParser(ILogger<CsvEmployeeParser> logger)`, `JsonEmployeeParser(ILogger<JsonEmployeeParser> logger)` — primary constructor로 변경.

- `Parse` 시작: `logger.LogDebug("{Format} 파싱 시작: ContentLength={ContentLength}", "CSV"/"JSON", content.Length)`
- `Parse` 완료: `logger.LogDebug("{Format} 파싱 완료: {Count}건", "CSV"/"JSON", result.Count)`
- catch: `logger.LogError(ex, "{Format} 파싱 중 오류 발생", "CSV"/"JSON")`

**Step 5: 빌드 및 테스트 확인**

Run: `dotnet build --verbosity quiet && dotnet test --verbosity quiet`
Expected: 빌드 성공, 테스트 통과

**Step 6: 커밋**

```bash
git add src/CompanyC.Api/
git commit -m "기능: 구조적 로깅 구현 - BeginScope/적절한 로그 레벨/템플릿 패턴 + FluentValidation 로깅"
```

---

## Task 5: 테스트 ErrorOr/FluentValidation 호환성 수정

**Files:**
- Modify: `tests/CompanyC.Api.IntegrationTests/EmployeeApiMockTests.cs`
- Modify: `tests/CompanyC.Api.IntegrationTests/GlobalUsings.cs`
- Modify: `tests/CompanyC.Api.IntegrationTests/CompanyC.Api.IntegrationTests.csproj`

**Step 1: 테스트 프로젝트에 ErrorOr 패키지 추가**

```bash
dotnet add tests/CompanyC.Api.IntegrationTests package ErrorOr
```

**Step 2: GlobalUsings에 추가**

```csharp
global using ErrorOr;
global using CompanyC.Api.Errors;
```

**Step 3: Mock 테스트를 ErrorOr 반환 타입에 맞게 수정**

`EmployeeApiMockTests.cs` 변경 예시:

```csharp
// GetEmployees → ErrorOr<GetEmployeesResult>
_mockGetEmployees
    .Setup(h => h.Handle(It.Is<GetEmployeesQuery>(q => q.Page == 2 && q.PageSize == 5)))
    .Returns(ErrorOrFactory.From(new GetEmployeesResult(employees.AsReadOnly(), 11)));

// GetByName 없는 경우 → ErrorOr<Employee> (Error)
_mockGetByName
    .Setup(h => h.Handle(It.Is<GetEmployeeByNameQuery>(q => q.Name == "없는사람")))
    .Returns(EmployeeErrors.NotFound("없는사람"));

// GetByName 있는 경우 → ErrorOr<Employee> (Value)
_mockGetByName
    .Setup(h => h.Handle(It.Is<GetEmployeeByNameQuery>(q => q.Name == "박모크")))
    .Returns(ErrorOrFactory.From(employee));

// AddEmployees → ErrorOr<List<Employee>>
_mockAddEmployees
    .Setup(h => h.Handle(It.IsAny<AddEmployeesCommand>()))
    .Returns(ErrorOrFactory.From(parsed));
```

**Step 4: NotFound 응답 형식 변경에 맞춘 Assert 수정**

기존 ErrorResponse → 새 `{ errors: [...] }` 형식이므로, StatusCode 검증 중심으로 변경 (body 형식 변경됨).

**Step 5: 빌드 및 전체 테스트**

Run: `dotnet build --verbosity quiet && dotnet test --verbosity normal`
Expected: 전체 통과

**Step 6: 커밋**

```bash
git add tests/CompanyC.Api.IntegrationTests/
git commit -m "테스트: ErrorOr/FluentValidation 호환 Mock/통합 테스트 수정"
```

---

## 최종 검증

**Step 1: 전체 빌드 + 테스트**

Run: `dotnet build --verbosity quiet && dotnet test --verbosity normal`
Expected: 빌드 성공, 모든 테스트 통과

**Step 2: API 수동 확인**

Run: `dotnet run --project src/CompanyC.Api`

- `GET /scalar/v1` — Scalar UI 정상 로딩
- `GET /api/employee?page=1&pageSize=10` — 200 OK + BeginScope 로그 출력
- `GET /api/employee/없는사람` — 404 + `{"errors":[{"code":"Employee.NotFound","description":"'없는사람' 이름의 직원을 찾을 수 없습니다."}]}`
- `POST /api/employee` empty body — 400 + `{"errors":[{"code":"Employee.EmptyBody","description":"..."}]}`
- `POST /api/employee` invalid email — 400 + `{"errors":[{"code":"Employee[0].Email","description":"올바른 이메일 형식이 아닙니다: ..."}]}`
- `POST /api/employee` invalid tel — 400 + `{"errors":[{"code":"Employee[0].Tel","description":"올바른 전화번호 형식이 아닙니다: ..."}]}`
- `POST /api/employee` invalid JSON — 500 + `{"errors":[{"code":"Server.UnexpectedError","description":"..."}]}` (스택 트레이스 없음)
- 콘솔에 `[Endpoint=POST /api/employee, ContentType=...]` BeginScope 로그 확인

**Step 3: 변경 요약**

| 항목 | Before | After |
|------|--------|-------|
| 에러 처리 | `ErrorResponse` record (단순 문자열) | ErrorOr + 도메인 에러 코드 |
| Handler 반환 | `List<Employee>`, `Employee?` | `ErrorOr<List<Employee>>`, `ErrorOr<Employee>` |
| 데이터 검증 | 없음 | FluentValidation (`EmployeeValidator`) |
| Email 검증 | `Contains('@')` (파싱 감지만) | FluentValidation `.EmailAddress()` |
| Tel 검증 | `IsTelNumber()` (파싱 감지만) | FluentValidation `.Matches()` 한국 전화번호 패턴 |
| 예외 처리 | 없음 (500 + 스택 트레이스) | try-catch → ErrorOr + 전역 예외 핸들러 |
| 로깅 | 전무 | 구조적 로깅 (BeginScope + 레벨별 + 템플릿) |
| 파일 접근 | `form.Files[0]` 직접 접근 | `form.Files.Count > 0` 안전 접근 |
| pageSize | 상한 없음 | 최대 100 |
| 요청 크기 | 제한 없음 | 10MB |
| 정규식 | `GeneratedRegex` (컬럼명 보안) | **유지** (FluentValidation과 별도 관심사) |
