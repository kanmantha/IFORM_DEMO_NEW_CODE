using System.Net;
using System.Text.Json;
using SiteQueryDefectTracking.Api.Common;
using SiteQueryDefectTracking.Application.Exceptions;

namespace SiteQueryDefectTracking.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (UnauthorizedException ex)
        {
            await WriteAsync(context, HttpStatusCode.Unauthorized, ex.Message);
        }
        catch (ForbiddenException ex)
        {
            await WriteAsync(context, HttpStatusCode.Forbidden, ex.Message);
        }
        catch (NotFoundException ex)
        {
            await WriteAsync(context, HttpStatusCode.NotFound, ex.Message);
        }
        catch (ConflictException ex)
        {
            await WriteAsync(context, HttpStatusCode.Conflict, ex.Message);
        }
        catch (ValidationException ex)
        {
            var errors = ex.Errors;
            await WriteAsync(context, HttpStatusCode.BadRequest, ex.Message, errors);
        }
        catch (BusinessException ex)
        {
            await WriteAsync(context, HttpStatusCode.BadRequest, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception for request {Path}", context.Request.Path);
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            var body = ApiResponse.Fail("An unexpected error occurred.");
            await WriteBodyAsync(context, HttpStatusCode.InternalServerError, body);
        }
    }

    private static async Task WriteAsync(
        HttpContext context,
        HttpStatusCode status,
        string message,
        IReadOnlyDictionary<string, string[]>? errors = null)
    {
        var body = ApiResponse.Fail(message, errors);
        await WriteBodyAsync(context, status, body);
    }

    private static Task WriteBodyAsync(HttpContext context, HttpStatusCode status, object body)
    {
        context.Response.Clear();
        context.Response.StatusCode = (int)status;
        context.Response.ContentType = "application/json; charset=utf-8";
        return context.Response.WriteAsync(JsonSerializer.Serialize(body, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        }));
    }
}