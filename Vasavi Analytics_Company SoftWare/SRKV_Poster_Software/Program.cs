using System.Net;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Authentication.Cookies;
using DailyPosterGenerator.Data;
using DailyPosterGenerator.Models;
using DailyPosterGenerator.Services;
using DailyPosterGenerator.Services.Auth;
using DailyPosterGenerator.Services.Email;
using DailyPosterGenerator.Services.MultiTenancy;
using DailyPosterGenerator.Services.Payments;
using DailyPosterGenerator.Services.Subscriptions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Database (SQL Server via EF Core).
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=(localdb)\\MSSQLLocalDB;Database=DailyPosterGenerator;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

// The factory is the single source of truth for DbContextOptions (avoids scoped/singleton
// conflicts when both scoped contexts and long-lived background services need EF Core).
builder.Services.AddDbContextFactory<DailyPosterDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddScoped<DailyPosterDbContext>(sp =>
    sp.GetRequiredService<IDbContextFactory<DailyPosterDbContext>>().CreateDbContext());

builder.Services.AddHttpClient();

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();

// Authentication: cookie for the MVC UI, JWT bearer for the SaaS API.
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtSigningKey = jwtSection["SigningKey"] ?? string.Empty;
if (jwtSigningKey.Length < 32)
{
    throw new InvalidOperationException("Jwt:SigningKey must be configured with at least 32 characters.");
}

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
        options.Cookie.Name = "DailyPoster.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"] ?? "DailyPosterGenerator",
            ValidAudience = jwtSection["Audience"] ?? "DailyPosterGenerator",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

// Application services.
builder.Services.AddSingleton<ISettingsService, SettingsService>();
builder.Services.AddHttpClient<IEventService, WikipediaEventService>();
builder.Services.AddHttpClient<OpenAiTextGenerationService>();
builder.Services.AddHttpClient("wiki", c =>
{
    c.DefaultRequestHeaders.UserAgent.ParseAdd("SRKV School Poster App/1.0 (contact: it@srkv.ac.in)");
    c.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddSingleton<TemplateTextGenerationService>();
builder.Services.AddSingleton<CompositeTextGenerationService>();
builder.Services.AddSingleton<ITextGenerationService>(sp => sp.GetRequiredService<CompositeTextGenerationService>());
builder.Services.AddSingleton<IPosterImageService, SkiaSharpPosterImageService>();
builder.Services.AddSingleton<ITemplateThumbnailService, TemplateThumbnailService>();
builder.Services.AddScoped<ITemplateImportService, TemplateImportService>();
builder.Services.AddSingleton<IPosterGenerationService, PosterGenerationService>();
builder.Services.AddSingleton<IPublishService, PublishService>();
builder.Services.AddSingleton<IActivityLog, ActivityLogService>();
builder.Services.AddHostedService<DailyPosterBackgroundService>();

// SaaS: auth.
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
builder.Services.AddScoped<IAuthService, AuthService>();

// SaaS: multi-tenancy + subscription state.
builder.Services.AddScoped<TenantContext>();

// SaaS: subscriptions, credits, features.
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<ICreditService, CreditService>();
builder.Services.AddScoped<IFeatureGateService, FeatureGateService>();

// SaaS: payments (mock for development; Razorpay added in the payments phase).
builder.Services.AddSingleton<IPaymentGateway, MockPaymentGateway>();

// SaaS: email (SMTP with log fallback).
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<IEmailService, EmailService>();

var app = builder.Build();

// Apply migrations and seed defaults.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DailyPosterDbContext>();
    await DbInitializer.InitializeAsync(db, scope.ServiceProvider.GetRequiredService<IConfiguration>());
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(appError =>
    {
        appError.Run(async context =>
        {
            var error = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
            if (error?.Error is null)
            {
                return;
            }

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;

            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "An unexpected error occurred.",
                    traceId = context.TraceIdentifier
                });
            }
            else
            {
                context.Response.Redirect("/Home/Error");
            }
        });
    });
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseMiddleware<SubscriptionStateMiddleware>();
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// If an older instance of this app is still holding one of our ports (e.g. a leftover
// process from a previous run), terminate it so startup never fails with "address already in use".
ReleasePortForPreviousInstance(ResolveConfiguredPorts());

app.Run();

// Returns every port the app is configured to listen on (multiple URLs may be
// separated by ';', e.g. "https://localhost:7238;http://localhost:5011").
List<int> ResolveConfiguredPorts()
{
    var url = builder.Configuration["urls"]
        ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
    if (string.IsNullOrWhiteSpace(url))
    {
        url = "http://localhost:5011";
    }

    var ports = new List<int>();
    foreach (var part in url.Split(';', StringSplitOptions.RemoveEmptyEntries))
    {
        var trimmed = part.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && uri.Port > 0 && !ports.Contains(uri.Port))
        {
            ports.Add(uri.Port);
        }
    }

    return ports.Count > 0 ? ports : new List<int> { 5011 };
}

// Kills any previous instance of this app that is still bound to one of the configured ports.
static void ReleasePortForPreviousInstance(List<int> ports)
{
    try
    {
        var stalePids = new HashSet<int>();
        foreach (var port in ports)
        {
            var heldBy = PortResolver.TryGetListenerPid(port);
            if (heldBy > 0)
            {
                stalePids.Add(heldBy);
            }
        }

        foreach (var pid in stalePids)
        {
            if (!IsSelfAppProcess(pid))
            {
                continue;
            }

            try
            {
                Console.WriteLine($"[DailyPosterGenerator] An older instance (PID {pid}) is still holding the configured port; terminating it before starting.");
                Process.GetProcessById(pid).Kill(entireProcessTree: true);
            }
            catch
            {
                // The process may have exited between lookup and kill; ignore.
            }
        }

        if (stalePids.Count > 0)
        {
            // Give the OS time to release the socket before Kestrel binds.
            Thread.Sleep(1200);
        }
    }
    catch
    {
        // Startup must never fail because of the port check.
    }
}

static bool IsSelfAppProcess(int pid)
{
    try
    {
        using var proc = Process.GetProcessById(pid);
        var name = proc.ProcessName;
        return name.Equals("dotnet", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("DailyPosterGenerator", StringComparison.OrdinalIgnoreCase);
    }
    catch
    {
        return false;
    }
}

internal static class PortResolver
{
    private const int AF_INET = 2;
    private const int TCP_TABLE_OWNER_PID_ALL = 5;
    private const uint MIB_TCP_STATE_LISTEN = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct MibTcprowOwnerPid
    {
        public uint dwState;
        public uint dwLocalAddr;
        public uint dwLocalPort;
        public uint dwRemoteAddr;
        public uint dwRemotePort;
        public uint dwOwningPid;
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(IntPtr pTcpTable, ref int pdwSize, bool bOrder, uint ulAf, int TableClass, uint reserved = 0);

    public static int TryGetListenerPid(int port)
    {
        int size = 0;
        GetExtendedTcpTable(IntPtr.Zero, ref size, false, AF_INET, TCP_TABLE_OWNER_PID_ALL);
        if (size <= 0)
        {
            return 0;
        }

        var buf = Marshal.AllocHGlobal(size);
        try
        {
            if (GetExtendedTcpTable(buf, ref size, false, AF_INET, TCP_TABLE_OWNER_PID_ALL) != 0)
            {
                return 0;
            }

            var rowSize = Marshal.SizeOf<MibTcprowOwnerPid>();
            var ptr = new IntPtr(buf.ToInt64() + sizeof(uint)); // first row starts after the entry count
            var count = Marshal.ReadInt32(buf);

            for (var i = 0; i < count; i++)
            {
                var row = Marshal.PtrToStructure<MibTcprowOwnerPid>(ptr);
                if (row.dwState == MIB_TCP_STATE_LISTEN
                    && IPAddress.NetworkToHostOrder((short)row.dwLocalPort) == port
                    && row.dwOwningPid > 0)
                {
                    return (int)row.dwOwningPid;
                }

                ptr = new IntPtr(ptr.ToInt64() + rowSize);
            }

            return 0;
        }
        finally
        {
            Marshal.FreeHGlobal(buf);
        }
    }
}
