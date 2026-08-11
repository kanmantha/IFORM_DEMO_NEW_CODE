using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiteQueryDefectTracking.Api.Common;
using SiteQueryDefectTracking.Application.DTOs.Email;
using SiteQueryDefectTracking.Application.Interfaces;

namespace SiteQueryDefectTracking.Api.Controllers;

[ApiController]
[Route("api/email")]
[Authorize(Policy = AppPolicies.CanManageEmails)]
public class EmailController(IEmailService emailService, IEmailTemplateService templates) : ControllerBase
{
    [HttpGet("templates")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<EmailTemplateDto>>>> Templates(CancellationToken ct)
    {
        var result = await templates.GetAllAsync(ct);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpPost("templates")]
    public async Task<ActionResult<ApiResponse<EmailTemplateDto>>> UpsertTemplate([FromBody] UpsertEmailTemplateRequest request, CancellationToken ct)
    {
        var result = await templates.UpsertAsync(request, ct);
        return Ok(ApiResponse.Ok(result, "Template saved."));
    }

    [HttpPost("generate")]
    public async Task<ActionResult<ApiResponse<GeneratedEmailDto>>> Generate([FromBody] GenerateEmailRequest request, CancellationToken ct)
    {
        var result = await emailService.GenerateAsync(request, ct);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpPost("send")]
    public async Task<ActionResult<ApiResponse<Guid>>> Send([FromBody] SendEmailRequest request, CancellationToken ct)
    {
        var id = await emailService.SendAsync(request, ct);
        return Ok(ApiResponse.Ok(id, "Email sent."));
    }

    [HttpPost("preview")]
    public async Task<ActionResult<ApiResponse<Guid>>> SendPreview([FromBody] SendPreviewRequest request, CancellationToken ct)
    {
        var id = await emailService.SendPreviewAsync(request, ct);
        return Ok(ApiResponse.Ok(id, "Preview email sent."));
    }
}