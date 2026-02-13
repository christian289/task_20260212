using CompanyC.Api.Models;

namespace CompanyC.Api.Validators;

public sealed class EmployeeValidator : AbstractValidator<Employee>
{
    public EmployeeValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("이름은 필수입니다.")
            .MaximumLength(100)
            .WithMessage("이름은 100자를 초과할 수 없습니다.");

        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("이메일은 필수입니다.")
            .EmailAddress()
            .WithMessage("올바른 이메일 형식이 아닙니다: '{PropertyValue}'");

        RuleFor(x => x.Tel)
            .NotEmpty()
            .WithMessage("전화번호는 필수입니다.")
            .Matches(@"^(01[016789])-?\d{3,4}-?\d{4}$")
            .WithMessage("올바른 전화번호 형식이 아닙니다: '{PropertyValue}' (예: 01012345678, 010-1234-5678)");

        RuleFor(x => x.Joined)
            .Must(d => d != default)
            .WithMessage("입사일이 유효하지 않습니다.")
            .LessThanOrEqualTo(DateTime.Now.Date.AddDays(1))
            .WithMessage("입사일은 미래일 수 없습니다.");
    }
}
