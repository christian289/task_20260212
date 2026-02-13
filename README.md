# CompanyC API - 직원 긴급 연락망 시스템

Company C 입사과제: 직원들의 긴급 연락망 구축을 위한 REST API

## 요구사항

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

## 빌드

```bash
dotnet build
```

## 실행

```bash
dotnet run --project src/CompanyC.Api
```

기본 실행 후 `http://localhost:5000` 에서 API를 사용할 수 있습니다.

API 문서는 `http://localhost:5000/scalar/v1` 에서 Scalar UI로 확인할 수 있습니다.

## 테스트

```bash
dotnet test
```

22개의 테스트가 실행됩니다 (통합 12 + Moq 4 + Bogus 6, `WebApplicationFactory` 기반, 별도 서버 실행 불필요).

## API 사용법

### 1. 직원 목록 조회 (페이징)

```bash
curl http://localhost:5000/api/employee?page=1&pageSize=10
```

응답 예시:
```json
{
  "page": 1,
  "pageSize": 10,
  "totalCount": 3,
  "totalPages": 1,
  "data": [
    { "name": "김철수", "email": "charles@clovf.com", "tel": "01075312468", "joined": "2018-03-07T00:00:00" }
  ]
}
```

### 2. 이름으로 직원 조회

```bash
curl http://localhost:5000/api/employee/김철수
```

- 성공: `200 OK` + 직원 정보
- 실패: `404 Not Found`

### 3. 직원 추가

#### CSV body 직접 입력

```bash
curl -X POST http://localhost:5000/api/employee \
  -H "Content-Type: text/csv" \
  -d "김철수, charles@clovf.com 01075312468, 2018.03.07
박영희, matilda@clovf.com 01087654321, 2021.04.28"
```

#### JSON body 직접 입력

```bash
curl -X POST http://localhost:5000/api/employee \
  -H "Content-Type: application/json" \
  -d '[{"name":"김클로","email":"clo@clovf.com","tel":"010-1111-2424","joined":"2012-01-05"}]'
```

#### CSV 파일 업로드

```bash
curl -X POST http://localhost:5000/api/employee \
  -F "file=@employees.csv;type=text/csv"
```

#### JSON 파일 업로드

```bash
curl -X POST http://localhost:5000/api/employee \
  -F "file=@employees.json;type=application/json"
```

- 성공: `201 Created` + 추가된 직원 수 및 데이터

## 더미 데이터 생성

`tools/CompanyC.DataGen`으로 한국식 직원 더미 데이터를 생성할 수 있습니다.

```bash
dotnet run --project tools/CompanyC.DataGen -- --count 50 --format both --output ./data
```

| 옵션 | 설명 | 기본값 |
|------|------|--------|
| `--count N` | 생성할 직원 수 | 10 |
| `--format csv\|json\|both` | 출력 형식 | both |
| `--output 경로` | 출력 디렉토리 | 현재 디렉토리 |

## 프로젝트 구조

```
CompanyC.slnx                          # 솔루션 파일
src/CompanyC.Api/                      # API 프로젝트 (Minimal API)
  GlobalUsings.cs                      # 전역 using 선언
  Program.cs                           # 엔드포인트 + DI + OpenAPI/Scalar
  Models/
    Employee.cs                        # Employee 클래스 (필수 필드 + ExtraFields)
  Parsers/
    IEmployeeParser.cs                 # 파서 인터페이스 (CanParse + Parse)
    CsvEmployeeParser.cs               # CSV 형식 파서
    JsonEmployeeParser.cs              # JSON 형식 파서 (알 수 없는 키 → ExtraFields)
  Repositories/
    IEmployeeRepository.cs             # 저장소 인터페이스 (데이터 접근)
    SqliteEmployeeRepository.cs        # SQLite 저장소 구현체
    EmployeeQueries.xml                # SQL 쿼리 (Content, 출력 디렉토리에 복사)
    QueryLoader.cs                     # XML 쿼리 로더
  Queries/
    GetEmployeesQuery.cs               # 쿼리: 페이지네이션 직원 목록 (요청 + 핸들러)
    GetEmployeeByNameQuery.cs          # 쿼리: 이름으로 직원 조회 (요청 + 핸들러)
  Commands/
    AddEmployeesCommand.cs             # 커맨드: CSV/JSON으로 직원 추가 (요청 + 핸들러)
tests/CompanyC.Api.IntegrationTests/   # 통합 테스트 (xUnit)
  GlobalUsings.cs                      # 전역 using 선언
  TestWebApplicationFactory.cs         # 격리된 테스트 팩토리 (임시 SQLite DB)
  EmployeeApiTests.cs                  # 통합 테스트 12개
  EmployeeApiMockTests.cs              # Moq 기반 단위 테스트 4개 (Handler 모킹)
  EmployeeBogusTests.cs                # Bogus 데이터 기반 테스트 6개
  EmployeeFaker.cs                     # Bogus 테스트 데이터 생성기 (CustomInstantiator)
tools/CompanyC.DataGen/                # CLI 더미 데이터 생성기
  GlobalUsings.cs                      # 전역 using 선언
  Program.cs                           # Bogus 기반 한국어 직원 데이터 생성
```

## 설계 결정사항

- **Minimal API**: 컨트롤러 없이 `Program.cs`에서 직접 엔드포인트 정의 (최소 파일 구성)
- **CQRS 패턴**: Query/Command 분리 — 요청 메시지 record + 핸들러 인터페이스/클래스 per 오퍼레이션
  - `GetEmployeesQuery` → `IGetEmployeesQueryHandler` → `GetEmployeesQueryHandler`
  - `GetEmployeeByNameQuery` → `IGetEmployeeByNameQueryHandler` → `GetEmployeeByNameQueryHandler`
  - `AddEmployeesCommand` → `IAddEmployeesCommandHandler` → `AddEmployeesCommandHandler`
- **SQLite 영속성**: Repository 패턴 (`IEmployeeRepository` → `SqliteEmployeeRepository`)
  - WAL 모드로 동시성 처리
  - ExtraFields를 단일 JSON 컬럼이 아닌 실제 DB 컬럼으로 동적 생성 (ALTER TABLE ADD COLUMN)
  - SELECT *로 읽은 후 기본 컬럼(Id, Name, Email, Tel, Joined) 외 컬럼은 ExtraFields에 로딩
- **외부 SQL 파일**: `Repositories/EmployeeQueries.xml`에서 쿼리 로드, DBA가 재컴파일 없이 수정 가능
- **전략 패턴 파서**: `IEmployeeParser` 인터페이스로 CSV/JSON 파서 교체 가능
  - `CsvEmployeeParser`: CSV/text/plain 파싱 (헤더 감지 시 ExtraFields 지원)
  - `JsonEmployeeParser`: JSON 파싱 (알 수 없는 키 → ExtraFields)
  - 새 형식 추가: `IEmployeeParser` 구현 + DI 등록
- **CSV 파싱**: 과제 문서의 CSV 형식에 맞춰 email과 tel이 공백으로 구분된 경우도 처리
- **Content-Type 추론**: 명시적 Content-Type이 없는 경우 `[` 또는 `{`로 시작하면 JSON, 그 외 CSV로 자동 판별
- **OpenAPI + Scalar**: API 문서 자동 생성 및 Scalar UI 제공 (`/scalar/v1`)
- **DTO는 record**: 불변성과 값 기반 동등성을 위해 모든 DTO를 `record`로 구현
- **JsonSerializerOptions 사전 정의**: 리플렉션 캐시 재사용을 위해 `static readonly`로 선언
- **CQRS Handler 인터페이스**: DI 기반 테스트(Moq) 지원을 위한 인터페이스 추출
- **전역 using 관리**: 외부 네임스페이스는 `GlobalUsings.cs`에 집중 관리

## ExtraFields 컬럼명 보안 (SQL Injection 방지)

CSV/JSON 입력에서 기본 필드(Name, Email, Tel, Joined) 외의 키는 `ExtraFields`로 처리되어 SQLite에 동적 컬럼으로 추가됩니다. 이때 SQL Injection을 방지하기 위해 **컬럼명 whitelist 검증**이 적용됩니다.

### 허용되는 컬럼명 규칙

| 규칙 | 설명 | 예시 |
|------|------|------|
| 문자 구성 | 영문(a-z, A-Z), 숫자(0-9), 밑줄(_)만 허용 (한글 불가) | `Department`, `team_name` |
| 첫 글자 | 숫자로 시작 불가 | `rank1` (O), `1rank` (X) |
| 길이 제한 | 최대 128자 | - |
| 제어 문자 | null 바이트 등 제어 문자 포함 불가 | - |
| 예약어 | 기본 컬럼명(Id, Name, Email, Tel, Joined) 사용 불가 | - |

### 유효한 ExtraFields 키 예시

```json
[
  {
    "name": "김철수",
    "email": "charles@clovf.com",
    "tel": "01075312468",
    "joined": "2018-03-07",
    "department": "개발팀",
    "position": "선임",
    "team_code": "A01"
  }
]
```

위 예시에서 `department`, `position`, `team_code`는 규칙에 부합하므로 DB 컬럼으로 동적 생성됩니다.

### 거부되는 키 (무시됨)

```json
{
  "x'); DROP TABLE Employees;--": "악성값",
  "col name with spaces": "공백 포함",
  "special!@#chars": "특수문자 포함",
  "직급": "한글 컬럼명 불가"
}
```

위 키들은 정규식 검증에 실패하여 **조용히 무시**됩니다 (예외 발생 없음, 정상 데이터는 저장됨).

### 검증 위치

컬럼명 검증은 `SqliteEmployeeRepository.IsValidColumnName()` 메서드에서 수행됩니다:

```
정규식 패턴: ^[a-zA-Z_][a-zA-Z_\d]*$
```

검증 규칙을 변경하려면 `Repositories/SqliteEmployeeRepository.cs`의 `SafeColumnNamePattern()`과 `IsValidColumnName()` 메서드를 수정하세요.
