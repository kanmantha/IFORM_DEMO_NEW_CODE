namespace SiteQueryDefectTracking.Application.Exceptions;

public class BusinessException : Exception
{
    public string Code { get; }

    public BusinessException(string message, string? code = null) : base(message)
    {
        Code = code ?? "BusinessError";
    }
}

public class NotFoundException : BusinessException
{
    public NotFoundException(string entityName, object id)
        : base($"{entityName} with id '{id}' was not found.", "NotFound")
    {
    }
}

public class ForbiddenException : BusinessException
{
    public ForbiddenException(string message = "You are not authorized to perform this action.")
        : base(message, "Forbidden") { }
}

public class UnauthorizedException : BusinessException
{
    public UnauthorizedException(string message = "Authentication is required.")
        : base(message, "Unauthorized") { }
}

public class ConflictException : BusinessException
{
    public ConflictException(string message) : base(message, "Conflict") { }
}

public class ValidationException : Exception
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationException(string message, IDictionary<string, string[]>? errors = null)
        : base(message)
    {
        Errors = errors as IReadOnlyDictionary<string, string[]> ?? new Dictionary<string, string[]>();
    }

    public ValidationException(IEnumerable<(string Property, string Error)> errors)
        : base("Validation failed.")
    {
        Errors = errors
            .GroupBy(e => e.Property, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Error).ToArray());
    }
}