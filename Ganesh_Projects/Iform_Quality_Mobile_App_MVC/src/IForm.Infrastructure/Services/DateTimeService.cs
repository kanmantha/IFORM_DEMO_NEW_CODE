using IForm.Contracts;

namespace IForm.Infrastructure.Services;

public class UtcDateTime : IDateTime
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
}
