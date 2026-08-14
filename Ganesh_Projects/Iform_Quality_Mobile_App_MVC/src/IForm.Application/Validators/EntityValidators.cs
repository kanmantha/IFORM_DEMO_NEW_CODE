using FluentValidation;
using IForm.Application.DTOs;
using IForm.Domain.Enums;

namespace IForm.Application.Validators;

public class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(x => x.ProductCode).NotEmpty().WithMessage("Product Code is required.").MaximumLength(50);
        RuleFor(x => x.ProductName).NotEmpty().WithMessage("Product Name is required.").MaximumLength(200);
        RuleFor(x => x.Specification).MaximumLength(500);
        RuleFor(x => x.Material).MaximumLength(100);
    }
}

public class CreateProjectRequestValidator : AbstractValidator<CreateProjectRequest>
{
    public CreateProjectRequestValidator()
    {
        RuleFor(x => x.ProjectCode).NotEmpty().WithMessage("Project Code is required.").MaximumLength(50);
        RuleFor(x => x.ProjectName).NotEmpty().WithMessage("Project Name is required.").MaximumLength(200);
        RuleFor(x => x.Status).IsInEnum();
    }
}

public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().WithMessage("Full name is required.").MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage("A valid email is required.");
        RuleFor(x => x.UserName).NotEmpty().WithMessage("User name is required.").MaximumLength(100);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).WithMessage("Password must be at least 8 characters.");
        RuleFor(x => x.Role).Must(r => AppRoles.All.Contains(r)).WithMessage("Role is not valid.");
    }
}

public class CreateEotRequestValidator : AbstractValidator<CreateEotRequest>
{
    public CreateEotRequestValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty().WithMessage("Project is required.");
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.Scenario).IsInEnum();
        RuleFor(x => x.FinancialYear).MaximumLength(20);
        RuleFor(x => x.ClientEotNumber).MaximumLength(100);
        RuleFor(x => x.Reason).MaximumLength(2000);
    }
}

public class AddScopeVariationRequestValidator : AbstractValidator<AddScopeVariationRequest>
{
    public AddScopeVariationRequestValidator()
    {
        RuleFor(x => x.ScopeAddition).GreaterThanOrEqualTo(0).WithMessage("Scope addition cannot be negative.");
        RuleFor(x => x.ScopeReduction).GreaterThanOrEqualTo(0).WithMessage("Scope reduction cannot be negative.");
        RuleFor(x => x.RevisionReference).MaximumLength(200);
    }
}

public class CreateTenantRequestValidator : AbstractValidator<CreateTenantRequest>
{
    public CreateTenantRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Tenant name is required.").MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage("A valid email is required.");
        RuleFor(x => x.PlanName).NotEmpty();
        RuleFor(x => x.TenantAdminName).NotEmpty().WithMessage("Admin name is required.");
        RuleFor(x => x.TenantAdminEmail).NotEmpty().EmailAddress().WithMessage("A valid admin email is required.");
        RuleFor(x => x.TenantAdminPassword).NotEmpty().MinimumLength(8).WithMessage("Password must be at least 8 characters.");
    }
}
