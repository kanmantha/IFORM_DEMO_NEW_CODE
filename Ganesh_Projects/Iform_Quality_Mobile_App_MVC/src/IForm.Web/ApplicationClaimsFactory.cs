using System.Security.Claims;
using IForm.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace IForm.Web;

/// <summary>
/// Adds tenant and display-name claims to the authentication cookie so the
/// ICurrentUser abstraction and tenant isolation filters work everywhere.
/// </summary>
public class ApplicationClaimsFactory : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole<Guid>>
{
    public ApplicationClaimsFactory(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole<Guid>> roleManager, IOptions<IdentityOptions> optionsAccessor)
        : base(userManager, roleManager, optionsAccessor)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        identity.AddClaim(new Claim("TenantId", user.TenantId.ToString()));
        identity.AddClaim(new Claim("FullName", user.FullName ?? string.Empty));

        return identity;
    }
}
