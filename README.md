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

기본 실행 후 `http://localhost:5012` 에서 API를 사용할 수 있습니다.

API 문서는 `http://localhost:5012/scalar/v1` 에서 Scalar UI로 확인할 수 있습니다.

## 테스트

```bash
dotnet test
```

26개의 테스트가 실행됩니다 (통합 16 + Moq 4 + Bogus 6, `WebApplicationFactory` 기반, 별도 서버 실행 불필요).

## API 사용법

### 1. 직원 목록 조회 (페이징)

```bash
curl http://localhost:5012/api/employee?page=1&pageSize=10
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
curl http://localhost:5012/api/employee/김철수
```

- 성공: `200 OK` + 직원 정보
- 실패: `404 Not Found` / `400 Bad Request` (빈값 또는 100자 초과)

### 3. 직원 추가

#### CSV body 직접 입력

```bash
curl -X POST http://localhost:5012/api/employee \
  -H "Content-Type: text/csv" \
  -d "김철수, charles@clovf.com 01075312468, 2018.03.07
박영희, matilda@clovf.com 01087654321, 2021.04.28"
```

#### JSON body 직접 입력

```bash
curl -X POST http://localhost:5012/api/employee \
  -H "Content-Type: application/json" \
  -d '[{"name":"김클로","email":"clo@clovf.com","tel":"010-1111-2424","joined":"2012-01-05"}]'
```

#### CSV 파일 업로드

```bash
curl -X POST http://localhost:5012/api/employee \
  -F "file=@employees.csv;type=text/csv"
```

#### JSON 파일 업로드

```bash
curl -X POST http://localhost:5012/api/employee \
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
  Program.cs                           # 엔드포인트 + DI + OpenAPI/Scalar + Serilog
  LogMessages.cs                       # [LoggerMessage] Source Generator 로그 메서드 정의
  Models/
    Employee.cs                        # Employee 클래스 (필수 필드 + ExtraFields)
  Parsers/
    IEmployeeParser.cs                 # 파서 인터페이스 (CanParse + Parse)
    CsvEmployeeParser.cs               # CSV 형식 파서
    JsonEmployeeParser.cs              # JSON 형식 파서 (알 수 없는 키 → ExtraFields)
    DateParsingHelper.cs               # 날짜 파싱 공유 헬퍼 (CSV/JSON 파서 공용)
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
  Validators/
    EmployeeValidator.cs               # FluentValidation 검증 규칙
  Errors/
    EmployeeErrors.cs                  # ErrorOr 에러 정의
tests/CompanyC.Api.IntegrationTests/   # 통합 테스트 (xUnit)
  GlobalUsings.cs                      # 전역 using 선언
  TestWebApplicationFactory.cs         # 격리된 테스트 팩토리 (임시 SQLite DB)
  EmployeeApiTests.cs                  # 통합 테스트 16개
  EmployeeApiMockTests.cs              # Moq 기반 단위 테스트 4개 (Handler 모킹)
  EmployeeBogusTests.cs                # Bogus 데이터 기반 테스트 6개
  EmployeeFaker.cs                     # Bogus 테스트 데이터 생성기 (CustomInstantiator)
  TestJsonOptions.cs                   # 테스트 공유 JsonSerializerOptions
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
  - Hash 기반 PK: `Name|Email|Tel|Joined`를 SHA256 해시하여 중복 데이터 방지 (`INSERT OR IGNORE`)
  - ExtraFields를 단일 JSON 컬럼이 아닌 실제 DB 컬럼으로 동적 생성 (ALTER TABLE ADD COLUMN, 트랜잭션 내 DDL)
  - SELECT *로 읽은 후 기본 컬럼(Hash, Name, Email, Tel, Joined) 외 컬럼은 ExtraFields에 로딩
  - `DateTime.TryParseExact`로 SQLite TEXT 값 안전 파싱 (SQLite에 DateTime 타입 없음)
- **보안**: 에러 응답에 내부 예외 메시지/DB 경로 미포함 — 상세 내용은 Serilog 로그에만 기록
- **입력 검증**: 페이지네이션 page 상한(10,000,000), name 길이 제한(100자), pageSize 범위 제한(1~100)
- **로깅**: Serilog (Console + 일별 롤링 파일 `logs/CompanyC-{date}.txt`, 30일 보관)
  - `[LoggerMessage]` Source Generator 패턴으로 고성능 구조화 로깅 (CA1848/CA1873 준수)
  - 모든 로그 메서드는 `LogMessages.cs`에 중앙 관리
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
- **LoggerMessage Source Generator**: `[LoggerMessage]` 어트리뷰트로 컴파일 타임 로그 코드 생성 (박싱/문자열 보간 오버헤드 제거)
- **CQRS Handler 인터페이스**: DI 기반 테스트(Moq) 지원을 위한 인터페이스 추출
- **Query Handler 예외 처리**: 모든 Query/Command 핸들러에서 `ErrorOr` 패턴으로 예외를 래핑하여 일관된 에러 응답 반환
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
| 예약어 | 기본 컬럼명(Hash, Name, Email, Tel, Joined) 사용 불가 | - |

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

## 동시성 테스트 (Singleton + SQLite WAL)

`tools/concurrent-test.ps1` 스크립트로 Singleton Repository + SQLite WAL 환경에서의 동시 쓰기 성능을 테스트했습니다.

```bash
pwsh -ExecutionPolicy Bypass -File "tools/concurrent-test.ps1"
```

### 테스트 시나리오

| 시나리오 | 동시 요청 | 요청당 직원 수 | 총 직원 수 | 결과 |
|----------|----------|---------------|-----------|------|
| 경량 동시성 | 100 | 1 | 100 | **PASS** - 100/100 성공, 6.7초 |
| 중량 동시성 | 100 | 50 | 5,000 | **PASS** - 100/100 성공, 8.6초 |
| 극단적 동시성 | 1,000 | 50 | 50,000 | **FAIL** - 65/1,000 성공, 9,500건 삽입 |

### 분석

**경량/중량 (PASS)**: SQLite WAL 모드가 동시 쓰기를 안정적으로 처리합니다.
- `INSERT OR IGNORE`로 Hash PK 기반 중복 방지
- 요청별 독립 `SqliteConnection`으로 교착(deadlock) 없음
- `Microsoft.Data.Sqlite` 내부 `busy_timeout`이 쓰기 잠금 대기 처리
- `SQLITE_BUSY` 에러 발생 없음

**극단적 (FAIL)**: SQLite 문제가 아닌 **Kestrel HTTP 서버 수용 한계**입니다.
- 1,000개 동시 TCP 연결 → 스레드 풀 포화
- `HttpClient.Timeout`(30초) 초과로 클라이언트측 요청 취소
- 서버에서 `BadHttpRequestException: Unexpected end of request content` 발생 (클라이언트 연결 끊김)
- 실제 프로덕션에서는 로드밸런서, rate limiting, 큐잉으로 처리하는 영역

### 결론

Singleton + SQLite WAL 구조는 **수백 건 수준의 동시 요청**에서 안정적으로 작동합니다. 극단적 부하(1,000+ 동시 요청)에서의 실패는 DB 레이어가 아닌 HTTP 서버 레이어의 한계이며, 이 규모에서는 DB를 PostgreSQL 등으로 전환하거나 앞단에 큐잉 시스템을 도입하는 것이 적절합니다.

## 파일 업로드 테스트 (CSV/JSON multipart/form-data)

서버 실행 상태에서 `curl`을 사용하여 CSV/JSON 파일 업로드 기능을 수동 검증했습니다.

### 테스트 결과

| 테스트 | 형식 | 결과 | HTTP |
|--------|------|------|------|
| CSV 파일 업로드 (5명) | multipart/form-data | 성공 | 201 |
| JSON 파일 업로드 (5명) | multipart/form-data | 성공 | 201 |
| CSV 중복 파일 재업로드 | multipart/form-data | 정상 거부 | 409 |
| JSON 중복 파일 재업로드 | multipart/form-data | 정상 거부 | 409 |
| CSV + ExtraFields (영문 헤더) | multipart/form-data | 성공 (department, position 저장) | 201 |
| JSON + ExtraFields | multipart/form-data | 성공 (department, position, note 저장) | 201 |
| 대용량 CSV (500명) | multipart/form-data | 성공 | 201 |
| 대용량 JSON (500명) | multipart/form-data | 성공 | 201 |
| 빈 파일 업로드 | multipart/form-data | 정상 거부 | 400 |
| 잘못된 확장자 (.txt) | multipart/form-data | 정상 거부 | 400 |
| 업로드 후 이름으로 조회 | GET | 정상 조회 | 200 |

### 테스트 명령 예시

```bash
# CSV 파일 업로드
curl -X POST http://localhost:5012/api/employee \
  -F "file=@employees.csv;filename=employees.csv"

# JSON 파일 업로드
curl -X POST http://localhost:5012/api/employee \
  -F "file=@employees.json;filename=employees.json"
```

### 검증 항목

- **정상 등록**: CSV/JSON 파일 모두 `201 Created` 반환, 한글 이름·이메일·전화번호·입사일 정상 파싱
- **ExtraFields**: JSON 파일의 추가 필드(department, position, note)가 DB 컬럼으로 동적 생성되어 저장됨
- **중복 방지**: 동일 파일 재업로드 시 `409 Conflict` 반환 (Hash PK 기반 `INSERT OR IGNORE`)
- **에러 처리**: 빈 파일 → `400` (NoFileUploaded), 지원하지 않는 확장자 → `400` (NoValidData)
- **대용량**: 500명 단위 파일도 정상 처리
- **DB 영속성**: 업로드 후 `GET /api/employee/{name}`으로 저장된 데이터 조회 확인

### 참고사항

CSV 파일에서 한글 헤더(`이름,이메일,전화번호,입사일`)를 사용할 경우, 파서가 heuristic 모드로 동작하여 ExtraFields가 무시됩니다. ExtraFields를 활용하려면 영문 헤더(`name,email,tel,joined,department,...`)를 사용해야 합니다.
