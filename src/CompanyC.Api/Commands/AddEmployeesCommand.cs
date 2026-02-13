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
    IValidator<Employee> employeeValidator,
    ILogger<AddEmployeesCommandHandler> logger) : IAddEmployeesCommandHandler
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
                foreach (var e in result.Errors)
                {
                    logger.LogDebug("검증 실패 [{Index}]: {Property} - {Message}",
                        i, e.PropertyName, e.ErrorMessage);
                }

                validationErrors.AddRange(result.Errors.Select(e =>
                    Error.Validation(
                        code: $"Employee[{i}].{e.PropertyName}",
                        description: e.ErrorMessage)));
            }
        }

        // 유효한 직원이 하나도 없으면 전체 검증 에러 반환
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
