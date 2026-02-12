# CompanyC API - Project Instructions

## Project Overview
Company C 입사과제 - 직원 긴급 연락망 API (ASP.NET Core Minimal API, .NET 10)

## Structure
```
CompanyC.slnx                          # Solution file
src/CompanyC.Api/                      # API project (Minimal API)
  Employee.cs                          # Employee model
  EmployeeService.cs                   # Business logic + CSV/JSON parsing
  Program.cs                           # Endpoints + DI configuration
tests/CompanyC.Api.IntegrationTests/   # Integration tests (xUnit)
  EmployeeApiTests.cs                  # 10 integration tests
```

## Build & Test
```bash
dotnet build
dotnet test
dotnet run --project src/CompanyC.Api
```

## API Endpoints
- `GET /api/employee?page={page}&pageSize={pageSize}` - paginated employee list
- `GET /api/employee/{name}` - employee lookup by name (404 if not found)
- `POST /api/employee` - add employees (CSV body, JSON body, CSV file, JSON file)

## Conventions
- Minimal API (no controllers) - keep file count minimal
- Singleton in-memory storage with thread-safe `Lock`
- CSV format: email and phone may be space-separated (e.g. `charles@clovf.com 01075312468`)
- Korean names supported (UTF-8)
- Integration tests use isolated `WebApplicationFactory` instances per test
