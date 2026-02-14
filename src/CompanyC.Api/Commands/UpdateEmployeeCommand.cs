using CompanyC.Api.Errors;
using CompanyC.Api.Models;
using CompanyC.Api.Repositories;

namespace CompanyC.Api.Commands;

public record UpdateEmployeeRequest(string? Name, string? Email, string? Tel, string? Joined);

public record UpdateEmployeeCommand(string CurrentName, UpdateEmployeeRequest Request);

public interface IUpdateEmployeeCommandHandler
{
    ErrorOr<Employee> Handle(UpdateEmployeeCommand command);
}

public sealed class UpdateEmployeeCommandHandler(
    IEmployeeRepository repository,
    IValidator<Employee> employeeValidator,
    ILogger<UpdateEmployeeCommandHandler> logger) : IUpdateEmployeeCommandHandler
{
    public ErrorOr<Employee> Handle(UpdateEmployeeCommand command)
    {
        try
        {
            logger.UpdateCommandExecuting(command.CurrentName);

            // 기존 직원 조회
            var existing = repository.GetByName(command.CurrentName);
            if (existing is null)
            {
                return EmployeeErrors.NotFound(command.CurrentName);
            }

            var currentHash = SqliteEmployeeRepository.ComputeHash(existing);
            var request = command.Request;

            // 요청된 필드만 업데이트 (null이면 기존 값 유지)
            var updatedName = string.IsNullOrWhiteSpace(request.Name) ? existing.Name : request.Name.Trim();
            var updatedEmail = string.IsNullOrWhiteSpace(request.Email) ? existing.Email : request.Email.Trim();
            var updatedTel = string.IsNullOrWhiteSpace(request.Tel) ? existing.Tel : request.Tel.Trim();
            var updatedJoined = existing.Joined;

            if (!string.IsNullOrWhiteSpace(request.Joined))
            {
                if (Parsers.DateParsingHelper.TryParseDate(request.Joined, out var parsedDate))
                    updatedJoined = parsedDate;
                else
                    return EmployeeErrors.ValidationFailed("입사일 형식이 올바르지 않습니다. (yyyy-MM-dd, yyyy.MM.dd, yyyy/MM/dd)");
            }

            var updated = new Employee
            {
                Name = updatedName,
                Email = updatedEmail,
                Tel = updatedTel,
                Joined = updatedJoined,
                ExtraFields = existing.ExtraFields
            };

            // FluentValidation 검증
            var validationResult = employeeValidator.Validate(updated);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors
                    .Select(e => Error.Validation(
                        code: $"Employee.{e.PropertyName}",
                        description: e.ErrorMessage))
                    .ToList();
                return errors;
            }

            var result = repository.Update(currentHash, updated);
            if (result.IsError)
                return result.Errors;

            logger.UpdateCommandCompleted(updated.Name);
            return updated;
        }
        catch (Exception ex)
        {
            logger.StorageError(ex);
            return EmployeeErrors.StorageFailed;
        }
    }
}
