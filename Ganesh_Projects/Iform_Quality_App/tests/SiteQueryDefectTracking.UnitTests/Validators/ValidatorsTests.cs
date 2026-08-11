using SiteQueryDefectTracking.Application.DTOs.Auth;
using SiteQueryDefectTracking.Application.DTOs.Email;
using SiteQueryDefectTracking.Application.DTOs.Products;
using SiteQueryDefectTracking.Application.DTOs.Queries;
using SiteQueryDefectTracking.Application.DTOs.Reports;
using SiteQueryDefectTracking.Application.Validators;

namespace SiteQueryDefectTracking.UnitTests.Validators;

public class CreateQueryRequestValidatorTests
{
    private readonly CreateQueryRequestValidator _sut = new();

    private static CreateQueryRequest Valid() => new()
    {
        ProjectId = Guid.NewGuid(),
        IssueTypeId = Guid.NewGuid(),
        IPO = "IPO-2026-001",
        QuantityNos = 10,
        QuantitySqm = 12.5m
    };

    [Fact]
    public void ValidRequest_Passes()
    {
        var result = _sut.Validate(Valid());
        Assert.True(result.IsValid);
    }

    [Fact]
    public void MissingProjectId_Fails()
    {
        var request = Valid();
        request.ProjectId = Guid.Empty;
        var result = _sut.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateQueryRequest.ProjectId));
    }

    [Fact]
    public void MissingIssueTypeId_Fails()
    {
        var request = Valid();
        request.IssueTypeId = Guid.Empty;
        var result = _sut.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateQueryRequest.IssueTypeId));
    }

    [Fact]
    public void MissingIpo_Fails()
    {
        var request = Valid();
        request.IPO = string.Empty;
        var result = _sut.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateQueryRequest.IPO));
    }

    [Fact]
    public void IpoOverMaxLength_Fails()
    {
        var request = Valid();
        request.IPO = new string('A', CreateQueryRequestValidator.MaxIpoLength + 1);
        var result = _sut.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateQueryRequest.IPO));
    }

    [Fact]
    public void NegativeQuantityNos_Fails()
    {
        var request = Valid();
        request.QuantityNos = -1;
        var result = _sut.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateQueryRequest.QuantityNos));
    }

    [Fact]
    public void NegativeQuantitySqm_Fails()
    {
        var request = Valid();
        request.QuantitySqm = -0.5m;
        var result = _sut.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateQueryRequest.QuantitySqm));
    }

    [Fact]
    public void NullQuantities_Fails()
    {
        var request = Valid();
        request.QuantityNos = null;
        request.QuantitySqm = null;
        var result = _sut.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateQueryRequest.QuantityNos));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateQueryRequest.QuantitySqm));
    }

    [Fact]
    public void ZeroQuantities_Passes()
    {
        var request = Valid();
        request.QuantityNos = 0;
        request.QuantitySqm = 0m;
        var result = _sut.Validate(request);
        Assert.True(result.IsValid);
    }
}

public class ReportRequestValidatorTests
{
    private readonly ReportRequestValidator _sut = new();

    [Fact]
    public void ValidRequest_Passes()
    {
        var request = new ReportRequest { Type = ReportType.OpenQueries, Format = ReportFormat.Csv };
        var result = _sut.Validate(request);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void InvalidTypeEnum_Fails()
    {
        var request = new ReportRequest { Type = (ReportType)99, Format = ReportFormat.Csv };
        var result = _sut.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(ReportRequest.Type));
    }

    [Fact]
    public void InvalidFormatEnum_Fails()
    {
        var request = new ReportRequest { Type = ReportType.OpenQueries, Format = (ReportFormat)99 };
        var result = _sut.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(ReportRequest.Format));
    }

    [Fact]
    public void ReversedDateRange_Fails()
    {
        var request = new ReportRequest
        {
            Type = ReportType.OpenQueries,
            From = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
            To = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero)
        };
        var result = _sut.Validate(request);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void NullDates_Passes()
    {
        var request = new ReportRequest { Type = ReportType.DelayReport, Format = ReportFormat.Excel };
        var result = _sut.Validate(request);
        Assert.True(result.IsValid);
    }
}

public class LoginRequestValidatorTests
{
    private readonly LoginRequestValidator _sut = new();

    [Fact]
    public void ValidLogin_Passes()
    {
        var result = _sut.Validate(new LoginRequest { UserNameOrEmail = "user@x.com", Password = "Secret123!" });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void MissingUsername_Fails()
    {
        var result = _sut.Validate(new LoginRequest { UserNameOrEmail = "", Password = "Secret123!" });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(LoginRequest.UserNameOrEmail));
    }

    [Fact]
    public void MissingPassword_Fails()
    {
        var result = _sut.Validate(new LoginRequest { UserNameOrEmail = "user@x.com", Password = "" });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(LoginRequest.Password));
    }
}

public class ChangePasswordRequestValidatorTests
{
    private readonly ChangePasswordRequestValidator _sut = new();

    [Fact]
    public void ShortNewPassword_Fails()
    {
        var result = _sut.Validate(new ChangePasswordRequest { CurrentPassword = "Old123!", NewPassword = "short" });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void MissingCurrentPassword_Fails()
    {
        var result = _sut.Validate(new ChangePasswordRequest { CurrentPassword = "", NewPassword = "LongEnough123!" });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidChange_Passes()
    {
        var result = _sut.Validate(new ChangePasswordRequest { CurrentPassword = "Old123!", NewPassword = "NewPassword123!" });
        Assert.True(result.IsValid);
    }
}

public class CreateProductRequestValidatorTests
{
    private readonly CreateProductRequestValidator _sut = new();

    [Fact]
    public void ValidProduct_Passes()
    {
        var result = _sut.Validate(new CreateProductRequest { Code = "P-001", Description = "A product" });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void MissingCode_Fails()
    {
        var result = _sut.Validate(new CreateProductRequest { Code = "", Description = "A product" });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateProductRequest.Code));
    }

    [Fact]
    public void MissingDescription_Fails()
    {
        var result = _sut.Validate(new CreateProductRequest { Code = "P-001", Description = "" });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateProductRequest.Description));
    }
}

public class CommentRequestValidatorTests
{
    private readonly CommentRequestValidator _sut = new();

    [Fact]
    public void EmptyComment_Fails()
    {
        var result = _sut.Validate(new AddCommentRequest { CommentText = "" });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AddCommentRequest.CommentText));
    }

    [Fact]
    public void OverlongComment_Fails()
    {
        var result = _sut.Validate(new AddCommentRequest { CommentText = new string('x', 2001) });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidComment_Passes()
    {
        var result = _sut.Validate(new AddCommentRequest { CommentText = "Looks good" });
        Assert.True(result.IsValid);
    }
}

public class UpsertEmailTemplateRequestValidatorTests
{
    private readonly UpsertEmailTemplateRequestValidator _sut = new();

    [Fact]
    public void ValidTemplate_Passes()
    {
        var result = _sut.Validate(new UpsertEmailTemplateRequest
        {
            Name = "Resolution",
            Code = "RESOLUTION",
            SubjectTemplate = "Query {{QUERY_NO}} resolved",
            BodyTemplate = "Dear {{SITE_ENGINEER}},"
        });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void MissingName_Fails()
    {
        var result = _sut.Validate(new UpsertEmailTemplateRequest { Name = "", Code = "X", SubjectTemplate = "s", BodyTemplate = "b" });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpsertEmailTemplateRequest.Name));
    }

    [Fact]
    public void MissingBody_Fails()
    {
        var result = _sut.Validate(new UpsertEmailTemplateRequest { Name = "N", Code = "X", SubjectTemplate = "s", BodyTemplate = "" });
        Assert.False(result.IsValid);
    }
}

public class SendEmailRequestValidatorTests
{
    private readonly SendEmailRequestValidator _sut = new();

    [Fact]
    public void ValidSend_Passes()
    {
        var result = _sut.Validate(new SendEmailRequest
        {
            QueryId = Guid.NewGuid(),
            Recipient = "manager@x.com",
            Subject = "Hello",
            Body = "World"
        });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void InvalidRecipient_Fails()
    {
        var result = _sut.Validate(new SendEmailRequest
        {
            QueryId = Guid.NewGuid(),
            Recipient = "not-an-email",
            Subject = "Hello",
            Body = "World"
        });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SendEmailRequest.Recipient));
    }

    [Fact]
    public void MissingQueryId_Fails()
    {
        var result = _sut.Validate(new SendEmailRequest
        {
            QueryId = Guid.Empty,
            Recipient = "manager@x.com",
            Subject = "Hello",
            Body = "World"
        });
        Assert.False(result.IsValid);
    }
}

public class CreateUserRequestValidatorTests
{
    private readonly CreateUserRequestValidator _sut = new();

    [Fact]
    public void ValidUser_Passes()
    {
        var result = _sut.Validate(new CreateUserRequest
        {
            FullName = "John Doe",
            UserName = "johndoe",
            Email = "john@x.com",
            Password = "Password123!",
            Roles = new[] { "Site Engineer" }
        });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void InvalidEmail_Fails()
    {
        var result = _sut.Validate(new CreateUserRequest
        {
            FullName = "John Doe",
            UserName = "johndoe",
            Email = "not-an-email",
            Password = "Password123!",
            Roles = new[] { "Manager" }
        });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateUserRequest.Email));
    }

    [Fact]
    public void NoRoles_Fails()
    {
        var result = _sut.Validate(new CreateUserRequest
        {
            FullName = "John Doe",
            UserName = "johndoe",
            Email = "john@x.com",
            Password = "Password123!",
            Roles = Array.Empty<string>()
        });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateUserRequest.Roles));
    }
}

public class ResetPasswordRequestValidatorTests
{
    private readonly ResetPasswordRequestValidator _sut = new();

    [Fact]
    public void ValidReset_Passes()
    {
        var result = _sut.Validate(new ResetPasswordRequest { UserId = Guid.NewGuid().ToString(), NewPassword = "NewPassword123!" });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void MissingUserId_Fails()
    {
        var result = _sut.Validate(new ResetPasswordRequest { UserId = "", NewPassword = "NewPassword123!" });
        Assert.False(result.IsValid);
    }
}

public class ProductSearchRequestValidatorTests
{
    private readonly ProductSearchRequestValidator _sut = new();

    [Fact]
    public void PageSizeAboveMax_Fails()
    {
        var result = _sut.Validate(new ProductSearchRequest { PageSize = 1000 });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(ProductSearchRequest.PageSize));
    }

    [Fact]
    public void ValidSearch_Passes()
    {
        var result = _sut.Validate(new ProductSearchRequest { PageSize = 25, Query = "bolt" });
        Assert.True(result.IsValid);
    }
}
