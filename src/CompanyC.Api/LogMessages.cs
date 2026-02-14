namespace CompanyC.Api;

internal static partial class LogMessages
{
    // === Repository ===
    [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "SQLite 데이터베이스 초기화 완료")]
    internal static partial void DatabaseInitialized(this ILogger logger);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Debug, Message = "{Count}건 INSERT 완료")]
    internal static partial void InsertCompleted(this ILogger logger, int count);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Error, Message = "직원 데이터 저장 중 오류 발생")]
    internal static partial void StorageError(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Warning, Message = "유효하지 않은 컬럼명 무시: {ColumnName}")]
    internal static partial void InvalidColumnNameIgnored(this ILogger logger, string columnName);

    [LoggerMessage(EventId = 1005, Level = LogLevel.Information, Message = "동적 컬럼 추가: {ColumnName}")]
    internal static partial void DynamicColumnAdded(this ILogger logger, string columnName);

    [LoggerMessage(EventId = 1006, Level = LogLevel.Debug, Message = "컬럼 이미 존재 (동시 요청): {ColumnName}")]
    internal static partial void ColumnAlreadyExists(this ILogger logger, string columnName);

    [LoggerMessage(EventId = 1007, Level = LogLevel.Debug, Message = "중복 직원 데이터 무시: {SkippedCount}건")]
    internal static partial void DuplicateEmployeesSkipped(this ILogger logger, int skippedCount);

    // === Endpoints - GET /api/employee ===
    [LoggerMessage(EventId = 2001, Level = LogLevel.Debug, Message = "직원 목록 조회 시작: Page={Page}, PageSize={PageSize}")]
    internal static partial void GetEmployeesStarted(this ILogger logger, int page, int pageSize);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Information, Message = "직원 목록 조회 완료: {ReturnedCount}/{TotalCount}건")]
    internal static partial void GetEmployeesCompleted(this ILogger logger, int returnedCount, int totalCount);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Warning, Message = "직원 목록 조회 실패: {ErrorCode} - {ErrorDescription}")]
    internal static partial void GetEmployeesFailed(this ILogger logger, string errorCode, string errorDescription);

    // === Endpoints - GET /api/employee/{name} ===
    [LoggerMessage(EventId = 2004, Level = LogLevel.Debug, Message = "이름으로 직원 조회 시작: Name={Name}")]
    internal static partial void GetEmployeeByNameStarted(this ILogger logger, string name);

    [LoggerMessage(EventId = 2005, Level = LogLevel.Information, Message = "직원 조회 성공: {EmployeeName}")]
    internal static partial void GetEmployeeByNameSuccess(this ILogger logger, string employeeName);

    [LoggerMessage(EventId = 2006, Level = LogLevel.Warning, Message = "직원 조회 실패: {ErrorCode} - {ErrorDescription}")]
    internal static partial void GetEmployeeByNameFailed(this ILogger logger, string errorCode, string errorDescription);

    // === Endpoints - POST /api/employee ===
    [LoggerMessage(EventId = 2007, Level = LogLevel.Warning, Message = "파일 업로드 실패: 파일 없음 또는 빈 파일")]
    internal static partial void FileUploadEmpty(this ILogger logger);

    [LoggerMessage(EventId = 2008, Level = LogLevel.Debug, Message = "파일 업로드 수신: FileName={FileName}, Size={FileSize}, Extension={Extension}")]
    internal static partial void FileUploadReceived(this ILogger logger, string fileName, long fileSize, string? extension);

    [LoggerMessage(EventId = 2009, Level = LogLevel.Warning, Message = "잘못된 multipart 요청: {ErrorMessage}")]
    internal static partial void InvalidMultipartRequest(this ILogger logger, string errorMessage);

    [LoggerMessage(EventId = 2010, Level = LogLevel.Warning, Message = "빈 요청 본문 수신")]
    internal static partial void EmptyRequestBody(this ILogger logger);

    [LoggerMessage(EventId = 2011, Level = LogLevel.Debug, Message = "본문 직접 입력 수신: ContentLength={ContentLength}")]
    internal static partial void DirectBodyReceived(this ILogger logger, int contentLength);

    [LoggerMessage(EventId = 2012, Level = LogLevel.Information, Message = "직원 {Count}명 등록 완료")]
    internal static partial void EmployeesRegistered(this ILogger logger, int count);

    [LoggerMessage(EventId = 2013, Level = LogLevel.Warning, Message = "직원 등록 실패: {ErrorCode} - {ErrorDescription}")]
    internal static partial void EmployeesRegistrationFailed(this ILogger logger, string errorCode, string errorDescription);

    // === Query Handlers ===
    [LoggerMessage(EventId = 3001, Level = LogLevel.Debug, Message = "DB 조회 실행: Page={Page}, PageSize={PageSize}")]
    internal static partial void DbQueryExecuting(this ILogger logger, int page, int pageSize);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Debug, Message = "DB 조회 완료: {ReturnedCount}/{TotalCount}건")]
    internal static partial void DbQueryCompleted(this ILogger logger, int returnedCount, int totalCount);

    [LoggerMessage(EventId = 3003, Level = LogLevel.Debug, Message = "이름 검색 실행: Name={Name}")]
    internal static partial void NameSearchExecuting(this ILogger logger, string name);

    [LoggerMessage(EventId = 3004, Level = LogLevel.Debug, Message = "직원 미발견: Name={Name}")]
    internal static partial void EmployeeNotFoundByName(this ILogger logger, string name);

    // === Command Handler ===
    [LoggerMessage(EventId = 4001, Level = LogLevel.Debug, Message = "명시적 파서 매칭 실패, Content Sniffing 시도: InferredType={InferredType}")]
    internal static partial void ContentSniffingFallback(this ILogger logger, string inferredType);

    [LoggerMessage(EventId = 4002, Level = LogLevel.Warning, Message = "파서를 찾을 수 없음: ContentType={ContentType}, FileExtension={FileExtension}")]
    internal static partial void NoParserFound(this ILogger logger, string? contentType, string? fileExtension);

    [LoggerMessage(EventId = 4003, Level = LogLevel.Debug, Message = "파서 선택: {ParserType}")]
    internal static partial void ParserSelected(this ILogger logger, string parserType);

    [LoggerMessage(EventId = 4004, Level = LogLevel.Error, Message = "파싱 실패: {ErrorCode} - {ErrorDescription}")]
    internal static partial void ParseFailed(this ILogger logger, string errorCode, string errorDescription);

    [LoggerMessage(EventId = 4005, Level = LogLevel.Warning, Message = "파싱 결과 유효한 직원 데이터 없음")]
    internal static partial void NoValidEmployeeData(this ILogger logger);

    [LoggerMessage(EventId = 4006, Level = LogLevel.Debug, Message = "검증 실패 [{Index}]: {Property} - {Message}")]
    internal static partial void ValidationFailedForEmployee(this ILogger logger, int index, string property, string message);

    [LoggerMessage(EventId = 4007, Level = LogLevel.Warning, Message = "전체 {Total}건 중 유효한 데이터 없음, 검증 에러 {ErrorCount}건")]
    internal static partial void AllValidationFailed(this ILogger logger, int total, int errorCount);

    [LoggerMessage(EventId = 4008, Level = LogLevel.Warning, Message = "부분 검증 성공: {ValidCount}/{TotalCount}건 유효, {ErrorCount}건 스킵")]
    internal static partial void PartialValidationSuccess(this ILogger logger, int validCount, int totalCount, int errorCount);

    [LoggerMessage(EventId = 4009, Level = LogLevel.Error, Message = "저장 실패: {ErrorCode} - {ErrorDescription}")]
    internal static partial void StoreFailed(this ILogger logger, string errorCode, string errorDescription);

    [LoggerMessage(EventId = 4010, Level = LogLevel.Information, Message = "직원 데이터 처리 완료: {Count}명 파싱/검증/저장 성공")]
    internal static partial void EmployeeDataProcessed(this ILogger logger, int count);

    // === CSV Parser ===
    [LoggerMessage(EventId = 5001, Level = LogLevel.Debug, Message = "CSV 파싱 시작: ContentLength={ContentLength}")]
    internal static partial void CsvParsingStarted(this ILogger logger, int contentLength);

    [LoggerMessage(EventId = 5002, Level = LogLevel.Debug, Message = "CSV 파싱 완료: {Count}건")]
    internal static partial void CsvParsingCompleted(this ILogger logger, int count);

    [LoggerMessage(EventId = 5003, Level = LogLevel.Error, Message = "CSV 파싱 중 오류 발생")]
    internal static partial void CsvParsingError(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 5007, Level = LogLevel.Debug, Message = "CSV 행 건너뜀 (필수 필드 누락): Line={LineNumber}")]
    internal static partial void CsvRowSkipped(this ILogger logger, int lineNumber);

    // === JSON Parser ===
    [LoggerMessage(EventId = 5004, Level = LogLevel.Debug, Message = "JSON 파싱 시작: ContentLength={ContentLength}")]
    internal static partial void JsonParsingStarted(this ILogger logger, int contentLength);

    [LoggerMessage(EventId = 5005, Level = LogLevel.Debug, Message = "JSON 파싱 완료: {Count}건")]
    internal static partial void JsonParsingCompleted(this ILogger logger, int count);

    [LoggerMessage(EventId = 5006, Level = LogLevel.Error, Message = "JSON 파싱 중 오류 발생")]
    internal static partial void JsonParsingError(this ILogger logger, Exception exception);
}
