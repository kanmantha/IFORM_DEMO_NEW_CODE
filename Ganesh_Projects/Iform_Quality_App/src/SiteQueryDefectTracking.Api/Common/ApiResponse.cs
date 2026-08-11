namespace SiteQueryDefectTracking.Api.Common;

public record ApiResponse<T>(
    bool Success,
    T? Data,
    string? Message);

public record ApiErrorResponse(
    bool Success,
    object? Data,
    string? Message,
    IReadOnlyDictionary<string, string[]>? Errors);

public static class ApiResponse
{
    public static ApiResponse<T> Ok<T>(T data, string? message = null) => new(true, data, message);

    public static ApiResponse<object?> Ok(string message) => new(true, null, message);

    public static ApiErrorResponse Fail(string message, IReadOnlyDictionary<string, string[]>? errors = null)
        => new(false, null, message, errors);
}