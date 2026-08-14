using IForm.Contracts;

namespace IForm.SecurityTests;

public sealed class TestCurrentUser : ICurrentUser
{
    public bool IsAuthenticated { get; set; } = true;
    public Guid? UserId { get; set; } = Guid.NewGuid();
    public Guid? TenantId { get; set; }
    public string? UserName { get; set; } = "test";
    public string? FullName { get; set; } = "Test User";
    public IEnumerable<string> Roles { get; set; } = new List<string>();
    public bool IsInRole(string role) => Roles.Contains(role);
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
