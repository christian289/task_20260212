# CompanyC API - 프로젝트 지침

## 프로젝트 개요
Company C 입사과제 - 직원 긴급 연락망 API (ASP.NET Core Minimal API, .NET 10)

## 요구사항 (과제 조건.md)
### 필수
- 직원 기본 연락 정보 조회 (GET 목록 + GET 이름검색)
- CSV/JSON 파일 업로드 및 body 직접 입력으로 직원 추가 (POST)
- CQRS 패턴 형태로 코드 구성
- 성공/실패 케이스 테스트 코드 작성

### 선택
- 로그 기능 구현
- OpenAPI를 이용한 API 명세 노출 (구현 완료: Scalar UI)
- 설계 변경 시 반영하기 쉬운 코드 형태

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
  EmployeeApiMockTests.cs             # Moq 기반 단위 테스트 4개 (Handler 모킹)
  EmployeeBogusTests.cs               # Bogus 데이터 기반 테스트 6개
  EmployeeFaker.cs                     # Bogus 테스트 데이터 생성기 (CustomInstantiator)
  TestJsonOptions.cs                   # 테스트 공유 JsonSerializerOptions
tools/CompanyC.DataGen/                # CLI 더미 데이터 생성기
  GlobalUsings.cs                      # 전역 using 선언
  Program.cs                           # Bogus 기반 한국어 직원 데이터 생성
tools/concurrent-test.ps1              # Singleton+WAL 동시성 테스트 스크립트
```

## 빌드 및 테스트
```bash
dotnet build
dotnet test                            # 테스트 26개 (통합 16 + Moq 4 + Bogus 6)
dotnet run --project src/CompanyC.Api  # API 서버 (Scalar UI: /scalar/v1)
dotnet run --project tools/CompanyC.DataGen -- --count 50 --format both
```

## API 엔드포인트
- `GET /api/employee?page={page}&pageSize={pageSize}` - 페이지네이션 직원 목록
- `GET /api/employee/{name}` - 이름으로 직원 조회 (미발견 시 404, 빈값/100자 초과 시 400)
- `POST /api/employee` - 직원 추가 (CSV 본문, JSON 본문, CSV 파일, JSON 파일)
- `GET /openapi/v1.json` - OpenAPI 명세
- `GET /scalar/v1` - Scalar API 문서 UI

## 아키텍처
- **CQRS**: Query/Command 분리 — 요청 메시지 record + 핸들러 인터페이스/클래스 per 오퍼레이션
  - `GetEmployeesQuery` → `IGetEmployeesQueryHandler` → `GetEmployeesQueryHandler` (try/catch + ErrorOr)
  - `GetEmployeeByNameQuery` → `IGetEmployeeByNameQueryHandler` → `GetEmployeeByNameQueryHandler` (try/catch + ErrorOr)
  - `AddEmployeesCommand` → `IAddEmployeesCommandHandler` → `AddEmployeesCommandHandler`
- **Employee**: 필수 필드(Name, Email, Tel, Joined) + `Dictionary<string, string> ExtraFields`를 가진 `sealed class` (`init` 속성으로 불변성 보장)
- **파서**: `IEmployeeParser` 인터페이스, `CanParse(contentType, extension)` 전략 패턴
  - `CsvEmployeeParser`: CSV/text/plain 파싱 (헤더 감지 시 ExtraFields 지원, 미감지 시 heuristic)
  - `JsonEmployeeParser`: JSON 파싱 (알 수 없는 키 → ExtraFields)
  - 새 형식 추가: `IEmployeeParser` 구현 + DI 등록
- **저장소**: `IEmployeeRepository` → `SqliteEmployeeRepository` (SQLite, WAL 모드, 동적 컬럼)
  - Hash 기반 PK: `Name|Email|Tel|Joined`를 SHA256 해시하여 중복 방지 (`INSERT OR IGNORE`, 전부 중복 시 409 Conflict)
  - ExtraFields는 단일 JSON 컬럼이 아닌 실제 DB 컬럼으로 동적 생성 (ALTER TABLE ADD COLUMN, 트랜잭션 내 DDL)
  - SELECT *로 읽은 후 기본 컬럼(Hash, Name, Email, Tel, Joined) 외 컬럼은 ExtraFields에 로딩
  - `DateTime.TryParseExact`로 SQLite TEXT 값 안전 파싱
- **보안**: 에러 응답에 내부 정보(ex.Message, DB 경로 등) 미포함 — 상세 내용은 로그에만 기록
- **입력 검증**: 페이지네이션 page 상한(10M), name 길이 제한(100자), pageSize 제한(1~100)
- **로깅**: Serilog (Console + 일별 롤링 파일 `logs/CompanyC-{date}.txt`, 30일 보관)
  - `[LoggerMessage]` Source Generator 패턴으로 고성능 로깅 (CA1848/CA1873 준수)
  - `LogMessages.cs`에 모든 로그 메서드 중앙 관리
  - `global using ILogger = Microsoft.Extensions.Logging.ILogger;` 별칭으로 Serilog ILogger 충돌 해소
- **JSON 인코딩**: `JavaScriptEncoder.Create(UnicodeRanges.All)`로 한글 직접 출력 (유니코드 이스케이프 없음)

## 규칙
- Minimal API (컨트롤러 없음)
- CQRS: Query Handler (읽기) / Command Handler (쓰기) 분리, Handler 인터페이스로 DI 테스트 지원
- SQLite 데이터 영속성 (Repository 패턴)
- SQL 쿼리는 `Repositories/EmployeeQueries.xml`에 저장 (Content 파일, 출력 디렉토리에 복사), 시작 시 `QueryLoader`로 로드
- DBA가 재컴파일 없이 쿼리 수정 가능한 구조 (외부 파일 기반)
- 연결 문자열: `Configuration.GetConnectionString("Default")`, 기본값: `Data Source=employees.db`
- CSV 형식: 이메일과 전화번호가 공백으로 구분될 수 있음 (예: `charles@clovf.com 01075312468`)
- 한국어 이름 지원 (UTF-8)
- 통합 테스트는 `TestWebApplicationFactory`로 테스트당 격리된 임시 SQLite DB 사용
- Bogus `Faker<Employee>`는 `CustomInstantiator` 사용 (필수 속성을 가진 클래스)

## Git 워크플로우 (브랜치 + PR)
- **main 브랜치**: 사용자 요청만 기록 (`[User Request]` auto-commit)
- **feature 브랜치**: Claude 작업 수행 (의미 있는 한글 커밋 메시지로 직접 커밋)
- 계획 수립 후 작업 시작 전: `git checkout -b feature/{작업-설명}` 으로 브랜치 생성
- **첫 번째 커밋**: Stop 훅이 자동으로 `[요구사항] {사용자 요청 내용}` 빈 커밋 생성 (auto-commit.mjs)
- 작업 중: 논리적 단위마다 WHY를 포함한 한글 커밋 수행
- 작업 완료 후: `gh pr create`로 PR 생성 (요약 + 의사결정 근거 포함)
- **커밋 메시지는 반드시 한글로 작성**
- 커밋 메시지 형식: `{타입}: {무엇을 했는지} - {왜 이렇게 했는지}`
  - 타입: 기능, 수정, 리팩토링, 테스트, 문서, 기타

## 코딩 표준 (스킬)
- 응답 DTO는 `record`으로 선언 (`class` 금지) - `.claude/skills/enforcing-dto-record/` 참조
- record 인스턴스화는 Named Arguments 생성자 방식 (속성 초기화 금지) - `.claude/skills/enforcing-record-constructor-initialization/` 참조
- `JsonSerializerOptions`는 `static readonly`로 선언 - `.claude/skills/enforcing-json-options-predefine/` 참조
- 외부 네임스페이스는 `GlobalUsings.cs`에 집중 관리 - `.claude/skills/managing-global-usings/` 참조
- `Regex`는 `[GeneratedRegex]` Source Generator로 선언 - `.claude/skills/enforcing-generated-regex/` 참조
- 로그 메서드는 `[LoggerMessage]` Source Generator로 `LogMessages.cs`에 정의 (CA1848/CA1873 준수)
- 브랜치 + PR 워크플로우 (의미 있는 커밋) - `.claude/skills/managing-branch-pr-workflow/` 참조
