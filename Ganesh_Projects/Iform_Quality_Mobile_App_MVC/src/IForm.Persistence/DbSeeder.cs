using System.Text.Json;
using IForm.Application.Services;
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
        var tenantId = await SeedDemoTenantAsync(context, userManager);
        await SeedDemoDataAsync(context, userManager, tenantId);
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

    private static async Task<Guid> SeedDemoTenantAsync(AppDbContext context, UserManager<ApplicationUser> userManager)
    {
        var existingTenant = await context.Tenants.FirstOrDefaultAsync(t => t.Slug == "i-form-aluminium");
        if (existingTenant != null) return existingTenant.Id;

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

        return tenant.Id;
    }

    private static async Task SeedDemoDataAsync(AppDbContext context, UserManager<ApplicationUser> userManager, Guid tenantId)
    {
        var manager = await EnsureUserAsync(userManager, tenantId, "manager", "manager@iform.example.com",
            "Mgr@12345", "I-FORM Manager", AppRoles.Manager);
        await EnsureUserAsync(userManager, tenantId, "engineer1", "engineer1@iform.example.com",
            "Eng@12345", "I-FORM Site Engineer 1", AppRoles.SiteEngineer);
        await EnsureUserAsync(userManager, tenantId, "engineer2", "engineer2@iform.example.com",
            "Eng2@12345", "I-FORM Site Engineer 2", AppRoles.SiteEngineer);

        if (!await context.Projects.IgnoreQueryFilters().AnyAsync(p => p.TenantId == tenantId))
        {
            var project = new Project
            {
                TenantId = tenantId,
                ProjectCode = "PRJ-1001",
                ProjectName = "I-FORM Demo Project",
                Client = "I-FORM Aluminium & Design LLP",
                Location = "India",
                Status = ProjectStatus.Active,
                AssignedManagerId = manager?.Id,
                StartDate = DateTime.UtcNow.AddDays(-30),
                PlannedCompletion = DateTime.UtcNow.AddDays(300)
            };
            context.Projects.Add(project);
            await context.SaveChangesAsync();

            context.Ipos.Add(new Ipo
            {
                TenantId = tenantId,
                IpoNumber = "IPO-1001",
                ProjectId = project.Id,
                Client = project.Client,
                Quantity = 100,
                QuantitySqm = 500,
                DispatchStatus = DispatchStatus.Pending,
                SlabTargetCastingDate = DateTime.UtcNow.AddDays(7)
            });
            await context.SaveChangesAsync();
        }

        if (!await context.Products.IgnoreQueryFilters().AnyAsync(p => p.TenantId == tenantId))
        {
            var photoCodes = new HashSet<string>
            {
                "DAAA", "DABA", "DACA", "DADA", "DAGA", "DAGB", "DAGC", "DAHA", "DAIB",
                "DBAA0000", "DCAC0059", "DCBC0157", "DCCE0001", "DDBA0001", "DDCF0092",
                "DECA0245", "DEDA0001", "DFAA", "DFAB1675", "DFAC1610", "DFAC1611",
                "DFAC1636", "DFAE", "DFAH2012", "DHBA0001", "DIBB0001", "DJBB0001",
                "DKAA", "DLAA0003", "DPAA0001", "DQAA18002000", "DQAF0600", "DQAG3000",
                "DRAA1710", "DRBA0001", "DRCA0002", "DRDA0001", "DRFA0001", "DRGA0001",
                "DRNA0004", "DROB0001", "DRTA0005", "DRVA0001", "DTGA0001", "DTGD",
                "DUAA0001", "DZAA", "DZAA0005"
            };

            foreach (var item in AccessoryCatalogue.All)
            {
                Guid? categoryId = null;
                if (!string.IsNullOrWhiteSpace(item.Category))
                {
                    var categoryName = item.Category.Trim();
                    var category = await context.ProductCategories
                        .IgnoreQueryFilters()
                        .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Name == categoryName);
                    if (category == null)
                    {
                        category = new ProductCategory { TenantId = tenantId, Name = categoryName };
                        context.ProductCategories.Add(category);
                        await context.SaveChangesAsync();
                    }
                    categoryId = category.Id;
                }

                context.Products.Add(new Product
                {
                    TenantId = tenantId,
                    ProductCode = item.Code,
                    ProductName = item.Name,
                    Specification = item.Specification,
                    Material = item.Material,
                    Unit = item.Unit,
                    Description = item.Description,
                    CategoryId = categoryId,
                    PhotoPath = photoCodes.Contains(item.Code) ? $"products/{item.Code}.jpg" : null,
                    Source = "iform-catalogue",
                    IsActive = true
                });
            }
            await context.SaveChangesAsync();
        }
    }

    private static async Task<ApplicationUser?> EnsureUserAsync(UserManager<ApplicationUser> userManager, Guid tenantId,
        string userName, string email, string password, string fullName, string role)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user != null) return user;

        user = new ApplicationUser
        {
            UserName = userName,
            Email = email,
            FullName = fullName,
            EmailConfirmed = true,
            TenantId = tenantId,
            IsActive = true,
            MustChangePassword = false
        };
        var result = await userManager.CreateAsync(user, password);
        if (result.Succeeded)
            await userManager.AddToRoleAsync(user, role);
        return user;
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
