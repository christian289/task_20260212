# CompanyC API - Project Instructions

## Project Overview
Company C 입사과제 - 직원 긴급 연락망 API (ASP.NET Core Minimal API, .NET 10)

## Structure
```
CompanyC.slnx                          # Solution file
src/CompanyC.Api/                      # API project (Minimal API)
  Employee.cs                          # Employee record model
  IEmployeeService.cs                  # Service interface (for DI/Moq)
  EmployeeService.cs                   # Business logic + CSV/JSON parsing
  Program.cs                           # Endpoints + DI + OpenAPI/Scalar
tests/CompanyC.Api.IntegrationTests/   # Integration tests (xUnit)
  EmployeeApiTests.cs                  # 10 integration tests
  EmployeeApiMockTests.cs             # 4 Moq-based unit tests
  EmployeeBogusTests.cs               # 6 Bogus data-driven tests
  EmployeeFaker.cs                     # Bogus test data generator
tools/CompanyC.DataGen/                # CLI dummy data generator
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

## Conventions
- Minimal API (no controllers) - keep file count minimal
- Singleton in-memory storage with thread-safe `Lock`
- CSV format: email and phone may be space-separated (e.g. `charles@clovf.com 01075312468`)
- Korean names supported (UTF-8)
- Integration tests use isolated `WebApplicationFactory` instances per test
- DTOs must be `record` (not `class`) - see `.claude/skills/enforcing-dto-record/`
- `JsonSerializerOptions` must be `static readonly` - see `.claude/skills/enforcing-json-options-predefine/`
- `IEmployeeService` interface for DI testability (Moq support)
