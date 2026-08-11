using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SiteQueryDefectTracking.Api.Common;
using SiteQueryDefectTracking.Application.Common;
using SiteQueryDefectTracking.Application.DTOs.Queries;
using SiteQueryDefectTracking.Application.Interfaces;

namespace SiteQueryDefectTracking.Api.Controllers;

[ApiController]
[Route("api/queries")]
[Authorize]
public class QueriesController(IQueryService services) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ApiResponse<Guid>>> Create([FromBody] CreateQueryRequest request, CancellationToken ct)
    {
        var id = await services.CreateAsync(request, ct);
        return Ok(ApiResponse.Ok(id));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<QueryDetailDto>>> Get([FromRoute] Guid id, CancellationToken ct)
    {
        var result = await services.GetAsync(id, ct);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<QuerySummaryDto>>>> Search(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? sortBy = "DelayDays",
        [FromQuery] string? sortDirection = "desc",
        [FromQuery] string? ipo = null,
        [FromQuery] string? keyword = null,
        [FromQuery] Guid? projectId = null,
        [FromQuery] Guid? issueTypeId = null,
        [FromQuery] SiteQueryDefectTracking.Domain.Enums.QueryStatus? status = null,
        [FromQuery] DateTimeOffset? dateFrom = null,
        [FromQuery] DateTimeOffset? dateTo = null,
        [FromQuery] string? raisedByUserId = null,
        [FromQuery] bool? mineOnly = null,
        CancellationToken ct = default)
    {
        var request = new QuerySearchRequest
        {
            Page = page,
            PageSize = pageSize,
            SortBy = sortBy,
            SortDirection = sortDirection,
            IPO = ipo,
            Keyword = keyword,
            ProjectId = projectId,
            IssueTypeId = issueTypeId,
            Status = status,
            DateFrom = dateFrom,
            DateTo = dateTo,
            RaisedByUserId = raisedByUserId,
            MineOnly = mineOnly
        };
        var result = await services.SearchAsync(request, ct);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<QuerySummaryDto>>> Update([FromRoute] Guid id, [FromBody] UpdateQueryRequest request, CancellationToken ct)
    {
        var result = await services.UpdateAsync(id, request, ct);
        return Ok(ApiResponse.Ok(result));
    }

    [Authorize(Policy = AppPolicies.CanResolveQueries)]
    [HttpPut("{id:guid}/status")]
    public async Task<ActionResult<ApiResponse<QuerySummaryDto>>> ChangeStatus([FromRoute] Guid id, [FromBody] ChangeQueryStatusRequest request, CancellationToken ct)
    {
        var result = await services.ChangeStatusAsync(id, request, ct);
        return Ok(ApiResponse.Ok(result));
    }

    [Authorize(Policy = AppPolicies.CanResolveQueries)]
    [HttpPut("{id:guid}/resolve")]
    public async Task<ActionResult<ApiResponse<Guid>>> Resolve([FromRoute] Guid id, [FromBody] ResolveQueryRequest request, CancellationToken ct)
    {
        var result = await services.ResolveAsync(id, request, ct);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpPost("{id:guid}/comments")]
    public async Task<ActionResult<ApiResponse<CommentDto>>> AddComment([FromRoute] Guid id, [FromBody] AddCommentRequest request, CancellationToken ct)
    {
        var result = await services.AddCommentAsync(id, request, ct);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpGet("{id:guid}/comments")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CommentDto>>>> GetComments([FromRoute] Guid id, CancellationToken ct)
    {
        var result = await services.GetCommentsAsync(id, ct);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpPost("{id:guid}/attachments")]
    [RequestSizeLimit(10L * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<AttachmentDto>>> UploadAttachment(
        [FromRoute] Guid id, [FromForm] IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(ApiResponse.Fail("A photo file is required."));
        }

        await using var stream = file.OpenReadStream();
        var result = await services.AddAttachmentAsync(id, stream, file.FileName, file.ContentType, ct);
        return Ok(ApiResponse.Ok(result, "Photo uploaded."));
    }

    [HttpGet("{id:guid}/attachments/{attachmentId:guid}/download")]
    public async Task<IActionResult> DownloadAttachment([FromRoute] Guid id, [FromRoute] Guid attachmentId, CancellationToken ct)
    {
        var (stream, contentType, fileName) = await services.GetAttachmentAsync(id, attachmentId, ct);
        return File(stream, contentType, fileName);
    }
}