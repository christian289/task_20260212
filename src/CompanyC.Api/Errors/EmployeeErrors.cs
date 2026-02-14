namespace CompanyC.Api.Errors;

public static class EmployeeErrors
{
    public static Error NotFound(string name) => Error.NotFound(
        code: "Employee.NotFound",
        description: "해당 이름의 직원을 찾을 수 없습니다.");

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

    public static Error ParseFailed(string format) => Error.Failure(
        code: "Employee.ParseFailed",
        description: $"{format} 파싱에 실패했습니다. 데이터 형식을 확인하세요.");

    public static readonly Error StorageFailed = Error.Unexpected(
        code: "Employee.StorageFailed",
        description: "데이터 저장 중 오류가 발생했습니다.");

    public static Error ValidationFailed(string details) => Error.Validation(
        code: "Employee.ValidationFailed",
        description: details);

    public static readonly Error AllDuplicate = Error.Conflict(
        code: "Employee.AllDuplicate",
        description: "모든 직원 데이터가 이미 등록되어 있습니다.");

    public static readonly Error InvalidName = Error.Validation(
        code: "Employee.InvalidName",
        description: "이름이 비어 있거나 100자를 초과할 수 없습니다.");
}
