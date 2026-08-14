namespace IForm.Domain.Exceptions;

public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}

public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}

public class AuthorizationException : Exception
{
    public AuthorizationException(string message) : base(message) { }
}

public class PlanLimitExceededException : Exception
{
    public PlanLimitExceededException(string message) : base(message) { }
}

public class ValidationException : Exception
{
    public ValidationException(string message) : base(message) { }
    public ValidationException(IEnumerable<string> errors) : base(string.Join("; ", errors)) { }
}
