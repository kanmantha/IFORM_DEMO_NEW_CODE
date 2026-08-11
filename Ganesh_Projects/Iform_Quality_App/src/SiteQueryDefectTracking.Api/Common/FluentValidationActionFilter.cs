using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;
using AppValidationException = SiteQueryDefectTracking.Application.Exceptions.ValidationException;

namespace SiteQueryDefectTracking.Api.Common;

/// <summary>
/// Validates action arguments against the FluentValidation validators registered
/// in the Application layer. The exception is converted to HTTP 400 with per-field
/// errors by <see cref="Middleware.ExceptionHandlingMiddleware"/>.
/// </summary>
public sealed class FluentValidationActionFilter : IAsyncActionFilter
{
    private readonly IServiceProvider _serviceProvider;

    public FluentValidationActionFilter(IServiceProvider serviceProvider)
        => _serviceProvider = serviceProvider;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null) continue;

            var validator = ResolveValidator(argument.GetType());
            if (validator is null) continue;

            var result = await validator.ValidateAsync(new ValidationContext<object>(argument));
            if (!result.IsValid)
            {
                throw new AppValidationException(result.Errors.Select(e => (e.PropertyName, e.ErrorMessage)));
            }
        }

        await next();
    }

    private IValidator? ResolveValidator(Type type)
    {
        var validatorType = typeof(IValidator<>).MakeGenericType(type);
        return _serviceProvider.GetService(validatorType) as IValidator;
    }
}
