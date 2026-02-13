# CompanyC API - Project Instructions

## Project Overview
Company C 입사과제 - 직원 긴급 연락망 API (ASP.NET Core Minimal API, .NET 10)

## Requirements (과제 조건.md)
### 필수
- 직원 기본 연락 정보 조회 (GET 목록 + GET 이름검색)
- CSV/JSON 파일 업로드 및 body 직접 입력으로 직원 추가 (POST)
- CQRS 패턴 형태로 코드 구성
- 성공/실패 케이스 테스트 코드 작성

### Optional
- 로그 기능 구현
- OpenAPI를 이용한 API spec 노출 (구현 완료: Scalar UI)
- 설계 변경 시 반영하기 쉬운 코드 형태

## Structure
```
CompanyC.slnx                          # Solution file
src/CompanyC.Api/                      # API project (Minimal API)
  GlobalUsings.cs                      # Global using declarations
  Employee.cs                          # Employee class (required fields + ExtraFields)
  IEmployeeParser.cs                   # Parser interface (CanParse + Parse)
  CsvEmployeeParser.cs                 # CSV format parser
  JsonEmployeeParser.cs                # JSON format parser (captures unknown keys)
  IEmployeeRepository.cs               # Repository interface (data access)
  SqliteEmployeeRepository.cs          # SQLite repository implementation
  EmployeeQueries.xml                  # SQL queries (Content, copied to output)
  QueryLoader.cs                       # XML query loader
  IEmployeeService.cs                  # Service interface (for DI/Moq)
  EmployeeService.cs                   # Business logic + parser orchestration
  Program.cs                           # Endpoints + DI + OpenAPI/Scalar
tests/CompanyC.Api.IntegrationTests/   # Integration tests (xUnit)
  GlobalUsings.cs                      # Global using declarations
  TestWebApplicationFactory.cs         # Isolated test factory (temp SQLite DB)
  EmployeeApiTests.cs                  # 10 integration tests
  EmployeeApiMockTests.cs             # 4 Moq-based unit tests
  EmployeeBogusTests.cs               # 6 Bogus data-driven tests
  EmployeeFaker.cs                     # Bogus test data generator (CustomInstantiator)
tools/CompanyC.DataGen/                # CLI dummy data generator
  GlobalUsings.cs                      # Global using declarations
  Program.cs                           # Bogus-based Korean employee data gen
```

## Build & Test
```bash
dotnet build
dotnet test                            # 20 tests (10 integration + 4 Moq + 6 Bogus)
dotnet run --project src/CompanyC.Api  # API server (Scalar UI at /scalar/v1)
dotnet run --project tools/CompanyC.DataGen -- --count 50 --format both
```

## API Endpoints
- `GET /api/employee?page={page}&pageSize={pageSize}` - paginated employee list
- `GET /api/employee/{name}` - employee lookup by name (404 if not found)
- `POST /api/employee` - add employees (CSV body, JSON body, CSV file, JSON file)
- `GET /openapi/v1.json` - OpenAPI specification
- `GET /scalar/v1` - Scalar API documentation UI

## Architecture
- **Employee**: `sealed class` with required fields (Name, Email, Phone, JoinedDate) + `Dictionary<string, string> ExtraFields`
- **Parser**: `IEmployeeParser` interface with `CanParse(contentType, extension)` strategy pattern
  - `CsvEmployeeParser`: CSV/text/plain parsing (heuristic token matching)
  - `JsonEmployeeParser`: JSON parsing (unknown keys → ExtraFields)
  - New formats: implement `IEmployeeParser` + register in DI
- **Repository**: `IEmployeeRepository` → `SqliteEmployeeRepository` (SQLite, WAL mode)
- **Service**: `IEmployeeService` → `EmployeeService` (parser orchestration + repository delegation)

## Conventions
- Minimal API (no controllers)
- SQLite data persistence via Repository pattern
- SQL queries stored in `EmployeeQueries.xml` (Content file, copied to output dir), loaded via `QueryLoader` at startup
- DBA가 재컴파일 없이 쿼리 수정 가능한 구조 (외부 파일 기반)
- Connection string from `Configuration.GetConnectionString("Default")`, default: `Data Source=employees.db`
- CSV format: email and phone may be space-separated (e.g. `charles@clovf.com 01075312468`)
- Korean names supported (UTF-8)
- Integration tests use `TestWebApplicationFactory` with isolated temp SQLite DB per test
- `IEmployeeService` interface for DI testability (Moq support)
- Bogus `Faker<Employee>` uses `CustomInstantiator` (class with required properties)

## Coding Standards (Skills)
- Response DTOs must be `record` (not `class`) - see `.claude/skills/enforcing-dto-record/`
- Record instantiation via constructor with Named Arguments (not property initializer) - see `.claude/skills/enforcing-record-constructor-initialization/`
- `JsonSerializerOptions` must be `static readonly` - see `.claude/skills/enforcing-json-options-predefine/`
- External namespaces centralized in `GlobalUsings.cs` - see `.claude/skills/managing-global-usings/`
