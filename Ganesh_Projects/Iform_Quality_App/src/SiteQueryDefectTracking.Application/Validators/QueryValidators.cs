namespace SiteQueryDefectTracking.Application.Validators;

using FluentValidation;

public class CreateQueryRequestValidator : AbstractValidator<DTOs.Queries.CreateQueryRequest>
{
    public const int MaxIpoLength = 60;

    public CreateQueryRequestValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty().WithMessage("Project is required.");
        RuleFor(x => x.IssueTypeId).NotEmpty().WithMessage("Issue type is required.");
        RuleFor(x => x.IPO)
            .NotEmpty().WithMessage("IPO number is required.")
            .MaximumLength(MaxIpoLength);
        RuleFor(x => x.QuantityNos).GreaterThanOrEqualTo(0).When(x => x.QuantityNos.HasValue);
        RuleFor(x => x.QuantitySqm).GreaterThanOrEqualTo(0).When(x => x.QuantitySqm.HasValue);

        // BRD: a query cannot be submitted without photo, quantity, project and IPO.
        RuleFor(x => x.QuantityNos).NotNull()
            .WithMessage("Quantity (Nos) is required.");
        RuleFor(x => x.QuantitySqm).NotNull()
            .WithMessage("Quantity (SQM) is required.");
    }
}