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

## 테스트

```bash
dotnet test
```

10개의 통합 테스트가 실행됩니다 (`WebApplicationFactory` 기반, 별도 서버 실행 불필요).

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
    { "name": "김철수", "email": "charles@clovf.com", "phone": "01075312468", "joinedDate": "2018-03-07T00:00:00" }
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

## 프로젝트 구조

```
CompanyC.slnx                          # 솔루션 파일
src/CompanyC.Api/
  CompanyC.Api.csproj                  # Web API 프로젝트 (.NET 10)
  Employee.cs                          # 직원 모델
  EmployeeService.cs                   # 비즈니스 로직 (CSV/JSON 파싱, 저장)
  Program.cs                           # API 엔드포인트 정의
tests/CompanyC.Api.IntegrationTests/
  CompanyC.Api.IntegrationTests.csproj # 통합 테스트 프로젝트
  EmployeeApiTests.cs                  # 10개 통합 테스트
```

## 설계 결정사항

- **Minimal API**: 컨트롤러 없이 `Program.cs`에서 직접 엔드포인트 정의 (최소 파일 구성)
- **In-Memory 저장소**: `EmployeeService`를 Singleton으로 등록하여 메모리에 데이터 저장
- **Thread-Safety**: `System.Threading.Lock`으로 동시 접근 보호
- **CSV 파싱**: 과제 문서의 CSV 형식에 맞춰 email과 phone이 공백으로 구분된 경우도 처리
- **Content-Type 추론**: 명시적 Content-Type이 없는 경우 `[` 또는 `{`로 시작하면 JSON, 그 외 CSV로 자동 판별
