using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using SiteQueryDefectTracking.Domain.Entities;
using SiteQueryDefectTracking.Infrastructure.Authentication;

namespace SiteQueryDefectTracking.UnitTests.Infrastructure;

public class TokenServiceTests
{
    private static TokenService CreateSut() => new(new JwtOptions
    {
        Issuer = "test-issuer",
        Audience = "test-audience",
        SecretKey = new string('x', 48),
        AccessTokenMinutes = 30,
        RefreshTokenDays = 14
    });

    private static User CreateUser(string id = "user-1", string email = "engineer@demo.local", string userName = "engineer") => new()
    {
        Id = id,
        Email = email,
        UserName = userName
    };

    [Fact]
    public void CreateAccessToken_ReturnsTokenAndExpiry()
    {
        var (token, expiresIn) = CreateSut().CreateAccessToken(CreateUser(), new[] { "Site Engineer" });

        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.Equal(1800, expiresIn);
    }

    [Fact]
    public void CreateAccessToken_IsSignedAndContainsClaims()
    {
        var sut = CreateSut();
        var user = CreateUser();
        var (token, _) = sut.CreateAccessToken(user, new[] { "Manager" });

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        Assert.Equal("test-issuer", jwt.Issuer);
        Assert.Contains("test-audience", jwt.Audiences);
        Assert.Equal(user.Id, jwt.Subject);
        Assert.Contains(jwt.Claims, c =>
            (c.Type == "role" || c.Type == System.Security.Claims.ClaimTypes.Role) && c.Value == "Manager");
        Assert.Contains(jwt.Claims, c =>
            (c.Type == JwtRegisteredClaimNames.Email || c.Type == "email") && c.Value == user.Email);
    }

    [Fact]
    public void CreateAccessToken_ValidatesSignature()
    {
        var sut = CreateSut();
        var (token, _) = sut.CreateAccessToken(CreateUser(), Array.Empty<string>());

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "test-issuer",
            ValidateAudience = true,
            ValidAudience = "test-audience",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(new string('x', 48))),
            ValidateLifetime = true,
            RoleClaimType = System.Security.Claims.ClaimTypes.Role
        };

        var handler = new JwtSecurityTokenHandler();
        var principal = handler.ValidateToken(token, parameters, out _);

        Assert.NotNull(principal);
    }

    [Fact]
    public void CreateRefreshToken_IsUniqueAndLong()
    {
        var sut = CreateSut();
        var a = sut.CreateRefreshToken();
        var b = sut.CreateRefreshToken();
        var bytes = System.Convert.FromBase64String(a);

        Assert.NotEqual(a, b);
        Assert.Equal(64, bytes.Length);
    }

    [Fact]
    public void Hash_IsDeterministic_AndNotReversible()
    {
        var h1 = TokenService.Hash("refresh-token-value");
        var h2 = TokenService.Hash("refresh-token-value");
        var h3 = TokenService.Hash("another-value");

        Assert.Equal(h1, h2);
        Assert.NotEqual(h1, h3);
        Assert.NotEqual("refresh-token-value", h1);
    }
}

public class SystemClockTests
{
    [Fact]
    public void Now_ReturnsUtcNow()
    {
        var clock = new SiteQueryDefectTracking.Infrastructure.Services.SystemClock();
        var diff = Math.Abs((clock.Now - DateTimeOffset.UtcNow).TotalSeconds);
        Assert.InRange(diff, 0, 5);
    }

    [Fact]
    public void UnknownTimeZone_FallsBackToUtc()
    {
        var clock = new SiteQueryDefectTracking.Infrastructure.Services.SystemClock("Not/AZone");
        Assert.True(clock.TryGetTimeZone("Not/AZone", out var resolved));
        Assert.Equal(TimeSpan.Zero, resolved!.GetUtcOffset(DateTime.UtcNow));
    }

    [Fact]
    public void BusinessTimeZone_ResolvesForValidId()
    {
        var clock = new SiteQueryDefectTracking.Infrastructure.Services.SystemClock();
        Assert.True(clock.TryGetTimeZone("Asia/Kolkata", out var tz));
        Assert.NotNull(tz);
    }
}

public class DelayCalculatorTests
{
    private sealed class FixedClock(DateTimeOffset now) : SiteQueryDefectTracking.Domain.Contracts.IClock
    {
        public DateTimeOffset Now => now;
        public DateTime Today => now.Date;
        public DateTimeOffset NowInBusinessTimeZone => now;
        public bool TryGetTimeZone(string timeZoneId, out TimeZoneInfo? timeZone) { timeZone = TimeZoneInfo.Utc; return true; }
    }

    [Fact]
    public void SameDay_IsZeroDays()
    {
        var now = DateTimeOffset.UtcNow;
        var sut = new SiteQueryDefectTracking.Infrastructure.Services.DelayCalculator(new FixedClock(now));
        Assert.Equal(0, sut.CalculateDelayDays(now));
    }

    [Fact]
    public void FiveDaysAgo_IsFiveDays()
    {
        var now = DateTimeOffset.UtcNow;
        var sut = new SiteQueryDefectTracking.Infrastructure.Services.DelayCalculator(new FixedClock(now));
        Assert.Equal(5, sut.CalculateDelayDays(now.AddDays(-5)));
    }

    [Fact]
    public void FutureRaiseDate_IsZero()
    {
        var now = DateTimeOffset.UtcNow;
        var sut = new SiteQueryDefectTracking.Infrastructure.Services.DelayCalculator(new FixedClock(now));
        Assert.Equal(0, sut.CalculateDelayDays(now.AddDays(3)));
    }

    [Fact]
    public void AsOf_Date_UsedWhenProvided()
    {
        var now = DateTimeOffset.UtcNow;
        var sut = new SiteQueryDefectTracking.Infrastructure.Services.DelayCalculator(new FixedClock(now));
        var raise = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var asOf = new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.Zero);
        Assert.Equal(9, sut.CalculateDelayDays(raise, asOf));
    }
}