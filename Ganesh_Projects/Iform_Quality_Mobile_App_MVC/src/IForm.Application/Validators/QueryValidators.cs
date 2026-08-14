using FluentValidation;
using IForm.Application.DTOs;

namespace IForm.Application.Validators;

public class CreateQueryRequestValidator : AbstractValidator<CreateQueryRequest>
{
    public CreateQueryRequestValidator()
    {
        RuleFor(x => x.IpoNumber).NotEmpty().WithMessage("IPO Number is required.").MaximumLength(50);
        RuleFor(x => x.ProjectId).NotEmpty().WithMessage("Project is required.");
        RuleFor(x => x.IssueType).IsInEnum().WithMessage("Please select an issue type.");
        RuleFor(x => x.QuantityNos).GreaterThan(0).When(x => x.QuantityNos.HasValue).WithMessage("Quantity (nos) must be greater than zero.");
        RuleFor(x => x.QuantitySqm).GreaterThan(0).When(x => x.QuantitySqm.HasValue).WithMessage("Quantity (sqm) must be greater than zero.");
        RuleFor(x => x.Comments).MaximumLength(4000);
        RuleFor(x => x.IpoNumber).NotEmpty().WithMessage("IPO Number is required.");
    }
}

public class AddCommentRequestValidator : AbstractValidator<AddCommentRequest>
{
    public AddCommentRequestValidator()
    {
        RuleFor(x => x.Body).NotEmpty().WithMessage("Comment cannot be empty.").MaximumLength(4000);
    }
}

public class CreateIpoRequestValidator : AbstractValidator<CreateIpoRequest>
{
    public CreateIpoRequestValidator()
    {
        RuleFor(x => x.IpoNumber).NotEmpty().WithMessage("IPO Number is required.").MaximumLength(50);
        RuleFor(x => x.ProjectId).NotEmpty().WithMessage("Project is required.");
    }
}
