using System.Text.Json;
using IForm.Domain.Entities;
using IForm.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IForm.Persistence;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole<Guid>> roleManager, string? superAdminEmail = null, string? superAdminPassword = null)
    {
        await context.Database.EnsureCreatedAsync();

        await SeedRolesAsync(roleManager);
        await SeedPlansAsync(context);
        await SeedSystemSettingsAsync(context);
        await SeedDemoTenantAsync(context, userManager);
        await SeedSuperAdminAsync(context, userManager, superAdminEmail ?? "superadmin@iform.example.com", superAdminPassword ?? "Admin@12345");
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole<Guid>> roleManager)
    {
        foreach (var role in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
        }
    }

    private static async Task SeedPlansAsync(AppDbContext context)
    {
        if (await context.SubscriptionPlans.AnyAsync()) return;

        var plans = new[]
        {
            new SubscriptionPlan
            {
                PlanName = "FREE", Tier = PlanTier.Free, Price = 0, Currency = "INR", BillingCycle = BillingCycle.Monthly,
                TrialDays = 0, MaxUsers = 5, MaxManagers = 1, MaxSiteEngineers = 4, MaxProjects = 2, MaxQueries = 100,
                MaxProducts = 100, MaxStorageBytes = 100L * 1024 * 1024, AllowEot = false, AllowDocuments = true,
                AllowAdvancedReports = false, AllowAuditLogs = true, AllowApiAccess = false, AllowCustomBranding = false,
                AllowEmailTemplates = true, IsActive = true, SortOrder = 0
            },
            new SubscriptionPlan
            {
                PlanName = "TRIAL", Tier = PlanTier.Trial, Price = 0, Currency = "INR", BillingCycle = BillingCycle.Monthly,
                TrialDays = 14, MaxUsers = 10, MaxManagers = 2, MaxSiteEngineers = 8, MaxProjects = 5, MaxQueries = 250,
                MaxProducts = 250, MaxStorageBytes = 500L * 1024 * 1024, AllowEot = true, AllowDocuments = true,
                AllowAdvancedReports = true, AllowAuditLogs = true, AllowApiAccess = false, AllowCustomBranding = false,
                AllowEmailTemplates = true, IsActive = true, SortOrder = 1
            },
            new SubscriptionPlan
            {
                PlanName = "STARTER", Tier = PlanTier.Starter, Price = 2500, Currency = "INR", BillingCycle = BillingCycle.Monthly,
                TrialDays = 14, MaxUsers = 25, MaxManagers = 5, MaxSiteEngineers = 20, MaxProjects = 15, MaxQueries = 1500,
                MaxProducts = 500, MaxStorageBytes = 2L * 1024 * 1024 * 1024, AllowEot = true, AllowDocuments = true,
                AllowAdvancedReports = true, AllowAuditLogs = true, AllowApiAccess = true, AllowCustomBranding = false,
                AllowEmailTemplates = true, IsActive = true, SortOrder = 2
            },
            new SubscriptionPlan
            {
                PlanName = "BUSINESS", Tier = PlanTier.Business, Price = 7500, Currency = "INR", BillingCycle = BillingCycle.Monthly,
                TrialDays = 14, MaxUsers = 100, MaxManagers = 20, MaxSiteEngineers = 80, MaxProjects = 100, MaxQueries = 10000,
                MaxProducts = 5000, MaxStorageBytes = 10L * 1024 * 1024 * 1024, AllowEot = true, AllowDocuments = true,
                AllowAdvancedReports = true, AllowAuditLogs = true, AllowApiAccess = true, AllowCustomBranding = true,
                AllowEmailTemplates = true, IsActive = true, SortOrder = 3
            },
            new SubscriptionPlan
            {
                PlanName = "ENTERPRISE", Tier = PlanTier.Enterprise, Price = 20000, Currency = "INR", BillingCycle = BillingCycle.Monthly,
                TrialDays = 14, MaxUsers = 10000, MaxManagers = 1000, MaxSiteEngineers = 9000, MaxProjects = 10000,
                MaxQueries = 1000000, MaxProducts = 100000, MaxStorageBytes = 100L * 1024 * 1024 * 1024,
                AllowEot = true, AllowDocuments = true, AllowAdvancedReports = true, AllowAuditLogs = true,
                AllowApiAccess = true, AllowCustomBranding = true, AllowEmailTemplates = true, IsActive = true, SortOrder = 4
            }
        };

        context.SubscriptionPlans.AddRange(plans);
        await context.SaveChangesAsync();
    }

    private static async Task SeedSystemSettingsAsync(AppDbContext context)
    {
        if (await context.SystemSettings.AnyAsync()) return;

        context.SystemSettings.AddRange(
            new SystemSetting { Key = "Platform.Name", Value = "I-FORM Site Query & Defect Tracking" },
            new SystemSetting { Key = "Platform.Currency", Value = "INR" },
            new SystemSetting { Key = "Platform.TimeZone", Value = "India Standard Time" },
            new SystemSetting { Key = "Platform.SupportEmail", Value = "support@iform.example.com" },
            new SystemSetting { Key = "Platform.DefaultTrialDays", Value = "14" },
            new SystemSetting { Key = "Platform.EnablePublicSignup", Value = "false" }
        );

        await context.SaveChangesAsync();
    }

    private static async Task SeedDemoTenantAsync(AppDbContext context, UserManager<ApplicationUser> userManager)
    {
        if (await context.Tenants.AnyAsync(t => t.Slug == "i-form-aluminium")) return;

        var tenant = new Tenant
        {
            Name = "I-FORM Aluminium & Design LLP",
            LegalName = "I-FORM Aluminium & Design LLP",
            Slug = "i-form-aluminium",
            Email = "admin@iform.example.com",
            Phone = "+91-0000000000",
            Address = "India",
            Country = "India",
            TimeZone = "India Standard Time",
            Status = TenantStatus.Active
        };
        context.Tenants.Add(tenant);
        await context.SaveChangesAsync();

        context.TenantSettings.AddRange(
            new TenantSetting { TenantId = tenant.Id, Key = "features", Value = "{}", SettingsJson = "{}" },
            new TenantSetting { TenantId = tenant.Id, Key = "severity", Value = "{}", SettingsJson = "{}" }
        );

        var trialPlan = await context.SubscriptionPlans.FirstAsync(p => p.Tier == PlanTier.Trial);
        var now = DateTime.UtcNow;
        context.Subscriptions.Add(new Subscription
        {
            TenantId = tenant.Id,
            PlanId = trialPlan.Id,
            Status = SubscriptionStatus.Trial,
            TrialStartDate = now,
            TrialEndDate = now.AddDays(trialPlan.TrialDays),
            StartDate = now,
            GracePeriodEndDate = now.AddDays(trialPlan.TrialDays + 7),
            PaymentStatus = PaymentStatus.FreeTrial,
            AutoRenew = true
        });

        context.UsageCounters.Add(new UsageCounter { TenantId = tenant.Id, LastCalculatedAt = now });

        SeedDefaultEmailTemplates(context, tenant.Id);

        await context.SaveChangesAsync();

        var adminEmail = "admin@iform.example.com";
        var admin = await userManager.FindByEmailAsync(adminEmail);
        if (admin == null)
        {
            admin = new ApplicationUser
            {
                UserName = "admin",
                Email = adminEmail,
                FullName = "I-FORM Tenant Admin",
                EmailConfirmed = true,
                TenantId = tenant.Id,
                IsActive = true
            };
            var result = await userManager.CreateAsync(admin, "Admin@12345");
            if (result.Succeeded)
                await userManager.AddToRoleAsync(admin, AppRoles.TenantAdmin);
        }
    }

    private static void SeedDefaultEmailTemplates(AppDbContext context, Guid tenantId)
    {
        var templates = new[]
        {
            new EmailTemplate
            {
                TenantId = tenantId, Name = "Missing Material Notification", IssueType = "Missing",
                Subject = "[{QueryNumber}] Missing Material - {ProductName} ({IpoNumber})",
                Body = @"Dear Sir,

Material with the following details was found missing at the site:

Project      : {Project}
IPO No.      : {IpoNumber}
Product Code : {ProductCode}
Product Name : {ProductName}
Quantity     : {Quantity}
Issue        : {IssueType}
Query No.    : {QueryNumber}
Raised On    : {Date}
Raised By    : {RaisedBy}

Please arrange for dispatch of the missing material at the earliest.

Thanks,
{TeamName}",
                IsDefault = true, IsActive = true, ToRecipients = "dispatch@iform.example.com"
            },
            new EmailTemplate
            {
                TenantId = tenantId, Name = "Production Mistake Notification", IssueType = "ProductionMistake",
                Subject = "[{QueryNumber}] Production Mistake - {ProductName} ({IpoNumber})",
                Body = @"Dear Sir,

The following production mistake was reported at the site:

Project      : {Project}
IPO No.      : {IpoNumber}
Product Code : {ProductCode}
Product Name : {ProductName}
Quantity     : {Quantity}
Issue        : {IssueType}
Query No.    : {QueryNumber}
Raised On    : {Date}
Raised By    : {RaisedBy}

Please review and advise the corrective action.

Thanks,
{TeamName}",
                IsDefault = true, IsActive = true, ToRecipients = "production@iform.example.com"
            },
            new EmailTemplate
            {
                TenantId = tenantId, Name = "Design Mistake Notification", IssueType = "DesignMistake",
                Subject = "[{QueryNumber}] Design Mistake - {ProductName} ({IpoNumber})",
                Body = @"Dear Sir,

The following design mistake was reported at the site:

Project      : {Project}
IPO No.      : {IpoNumber}
Product Code : {ProductCode}
Product Name : {ProductName}
Quantity     : {Quantity}
Issue        : {IssueType}
Query No.    : {QueryNumber}
Raised On    : {Date}
Raised By    : {RaisedBy}

Please review the drawings and advise the corrective action.

Thanks,
{TeamName}",
                IsDefault = true, IsActive = true, ToRecipients = "design@iform.example.com"
            },
            new EmailTemplate
            {
                TenantId = tenantId, Name = "Dispatch Missing Notification", IssueType = "DispatchMissing",
                Subject = "[{QueryNumber}] Dispatch Missing - {ProductName} ({IpoNumber})",
                Body = @"Dear Sir,

The following material was reported as missing in dispatch at the site:

Project      : {Project}
IPO No.      : {IpoNumber}
Product Code : {ProductCode}
Product Name : {ProductName}
Quantity     : {Quantity}
Issue        : {IssueType}
Query No.    : {QueryNumber}
Raised On    : {Date}
Raised By    : {RaisedBy}

Please verify the dispatch records and arrange the same.

Thanks,
{TeamName}",
                IsDefault = true, IsActive = true, ToRecipients = "dispatch@iform.example.com"
            }
        };

        context.EmailTemplates.AddRange(templates);
    }

    private static async Task SeedSuperAdminAsync(AppDbContext context, UserManager<ApplicationUser> userManager, string email, string password)
    {
        // Platform tenant owned by the SuperAdmin (TenantId = Guid.Empty). Needed
        // so AspNetUsers.TenantId FK to Tenants.Id resolves for platform-level users.
        var platform = await context.Tenants.FirstOrDefaultAsync(t => t.Slug == "platform");
        if (platform == null || platform.Id != Guid.Empty)
        {
            if (platform != null)
            {
                context.Tenants.Remove(platform);
                await context.SaveChangesAsync();
            }
            platform = new Tenant
            {
                Id = Guid.Empty,
                Name = "I-FORM Platform",
                Slug = "platform",
                Email = email,
                Status = TenantStatus.Active,
                CreatedAt = DateTime.UtcNow
            };
            context.Tenants.Add(platform);
            await context.SaveChangesAsync();
        }

        var superAdmin = await userManager.FindByEmailAsync(email);
        if (superAdmin != null) return;

        superAdmin = new ApplicationUser
        {
            UserName = "superadmin",
            Email = email,
            FullName = "Platform Super Admin",
            EmailConfirmed = true,
            TenantId = Guid.Empty,
            IsActive = true
        };
        var result = await userManager.CreateAsync(superAdmin, password);
        if (result.Succeeded)
            await userManager.AddToRoleAsync(superAdmin, AppRoles.SuperAdmin);
    }
}
