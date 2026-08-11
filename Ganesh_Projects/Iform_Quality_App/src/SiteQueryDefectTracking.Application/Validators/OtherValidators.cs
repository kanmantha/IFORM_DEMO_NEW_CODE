namespace SiteQueryDefectTracking.Application.Validators;

using FluentValidation;

public class ProductSearchRequestValidator : AbstractValidator<DTOs.Products.ProductSearchRequest>
{
    public ProductSearchRequestValidator()
    {
        RuleFor(x => x.Query).MaximumLength(150);
        RuleFor(x => x.PageSize).InclusiveBetween(1, Common.Pagination.MaxPageSize);
    }
}

public class CreateProductRequestValidator : AbstractValidator<DTOs.Products.CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Barcode).MaximumLength(80);
    }
}

public class CommentRequestValidator : AbstractValidator<DTOs.Queries.AddCommentRequest>
{
    public CommentRequestValidator()
    {
        RuleFor(x => x.CommentText).NotEmpty().MaximumLength(2000);
    }
}

public class UpsertEmailTemplateRequestValidator : AbstractValidator<DTOs.Email.UpsertEmailTemplateRequest>
{
    public UpsertEmailTemplateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(60);
        RuleFor(x => x.SubjectTemplate).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BodyTemplate).NotEmpty();
    }
}

public class GenerateEmailRequestValidator : AbstractValidator<DTOs.Email.GenerateEmailRequest>
{
    public GenerateEmailRequestValidator()
    {
        RuleFor(x => x.QueryId).NotEmpty();
    }
}

public class SendEmailRequestValidator : AbstractValidator<DTOs.Email.SendEmailRequest>
{
    public SendEmailRequestValidator()
    {
        RuleFor(x => x.QueryId).NotEmpty();
        RuleFor(x => x.Recipient).NotEmpty().EmailAddress().MaximumLength(300);
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Body).NotEmpty();
    }
}

public class ReportRequestValidator : AbstractValidator<DTOs.Reports.ReportRequest>
{
    public ReportRequestValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Format).IsInEnum();
        RuleFor(x => x).Must(r => r.From is null || r.To is null || r.From <= r.To)
            .WithMessage("Date range is invalid.");
    }
}