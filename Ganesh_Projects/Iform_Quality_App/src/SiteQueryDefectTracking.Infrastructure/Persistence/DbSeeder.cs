using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SiteQueryDefectTracking.Domain.Constants;
using SiteQueryDefectTracking.Domain.Entities;
using SiteQueryDefectTracking.Domain.Enums;
using SiteQueryDefectTracking.Infrastructure.Persistence;

namespace SiteQueryDefectTracking.Infrastructure.Persistence;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var context = services.GetRequiredService<AppDbContext>();
        var configuration = services.GetRequiredService<IConfiguration>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");
        var userManager = services.GetRequiredService<UserManager<User>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        if ((await context.Database.GetPendingMigrationsAsync()).Any())
        {
            logger.LogInformation("Applying pending migrations...");
            await context.Database.MigrateAsync();
        }

        logger.LogInformation("Ensuring roles and users...");
        await EnsureRoleAsync(roleManager, AppRoles.Manager);
        await EnsureRoleAsync(roleManager, AppRoles.SiteEngineer);

        var defaultPassword = configuration["Seed:DefaultPassword"] ?? "Demo@1234!";

        var manager = await EnsureUserAsync(userManager, "manager@demo.local", "Manager", "Demo", "Manager", defaultPassword, new[] { AppRoles.Manager });
        var engineer = await EnsureUserAsync(userManager, "siteengineer@demo.local", "Site", "Engineer", "Engineer", defaultPassword, new[] { AppRoles.SiteEngineer });
        var secondEngineer = await EnsureUserAsync(userManager, "engineer2@demo.local", "Ravi", "Kumar", "Engineer", defaultPassword, new[] { AppRoles.SiteEngineer });

        logger.LogInformation("Seeding reference data...");
        var issueTypes = await SeedIssueTypesAsync(context);

        var projects = await SeedProjectsAsync(context);

        var productCodes = await SeedCatalogueAsync(context);

        await SeedEmailTemplatesAsync(context, issueTypes);

        if (!await context.Queries.AnyAsync())
        {
            logger.LogInformation("Seeding demo queries...");
            SeedQueries(context, manager, secondEngineer, issueTypes, projects, productCodes);
            await context.SaveChangesAsync();
        }

        logger.LogInformation("Seed completed.");
    }

    private static async Task EnsureRoleAsync(RoleManager<IdentityRole> roleManager, string role)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    private static async Task<User> EnsureUserAsync(UserManager<User> userManager, string email, string firstName, string lastName, string display, string password, string[] roles)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new User
            {
                Id = System.Guid.NewGuid().ToString(),
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = firstName,
                LastName = lastName,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            };
            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Failed to create user {email}: {string.Join("; ", result.Errors.Select(e => e.Description))}");
            }
        }

        foreach (var role in roles)
        {
            if (!await userManager.IsInRoleAsync(user, role))
            {
                await userManager.AddToRoleAsync(user, role);
            }
        }

        return user;
    }

    private static async Task<List<IssueType>> SeedIssueTypesAsync(AppDbContext context)
    {
        var codeName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [IssueTypeCodes.Missing] = "Missing",
            [IssueTypeCodes.ProductionMistake] = "Production Mistake",
            [IssueTypeCodes.DesignMistake] = "Design Mistake",
            [IssueTypeCodes.DispatchMissing] = "Dispatch Missing"
        };

        var seeded = new List<IssueType>();
        foreach (var (code, name) in codeName)
        {
            var existing = await context.IssueTypes.FirstOrDefaultAsync(i => i.Code == code);
            if (existing is null)
            {
                existing = new IssueType { Code = code, Name = name, IsActive = true };
                context.IssueTypes.Add(existing);
                await context.SaveChangesAsync();
            }

            seeded.Add(existing);
        }

        return seeded;
    }

    private static async Task<List<Project>> SeedProjectsAsync(AppDbContext context)
    {
        var definitions = new List<(string Code, string Name, string Client, string Location)>
        {
            ("PRJ-CHN-001", "Park View Residency", "Shanmuga Constructions", "Chennai"),
            ("PRJ-BLR-001", "Azure Heights", "Prestige Builders", "Bengaluru"),
            ("PRJ-HYD-001", "Green Meadows", "L&T Construction", "Hyderabad"),
            ("PRJ-BOM-001", "Skyline Towers", "Oberoi Realty", "Mumbai"),
            ("PRJ-CBE-001", "Riverside Villas", "Accent Homes", "Coimbatore")
        };

        var seeded = new List<Project>();
        foreach (var (code, name, client, location) in definitions)
        {
            var project = await context.Projects.FirstOrDefaultAsync(p => p.Code == code);
            if (project is null)
            {
                project = new Project { Code = code, Name = name, ClientName = client, Location = location, IsActive = true };
                context.Projects.Add(project);
            }
            else
            {
                project.Name = name;
                project.ClientName = client;
                project.Location = location;
                project.IsActive = true;
            }

            seeded.Add(project);
        }

        await context.SaveChangesAsync();
        return seeded;
    }

    public static async Task<List<ProductCode>> SeedCatalogueAsync(AppDbContext context)
    {
        var catalogue = AccessoriesCatalogue.All;
        List<ProductCode> result = new();

        foreach (var item in catalogue)
        {
            var product = await context.ProductCodes.FirstOrDefaultAsync(p => p.Code == item.Code);
            if (product is null)
            {
                product = new ProductCode
                {
                    Code = item.Code,
                    Name = item.Name,
                    Description = item.Name,
                    Specification = item.Specification,
                    Material = item.Material,
                    Category = item.Category,
                    Unit = item.Unit,
                    IsActive = true,
                    IsVerified = true,
                    LastImportedAt = DateTimeOffset.UtcNow
                };
                context.ProductCodes.Add(product);
                result.Add(product);
            }
            else
            {
                product.Name = item.Name;
                product.Description = item.Name;
                product.Specification = item.Specification;
                product.Material = item.Material;
                product.Category = item.Category;
                product.Unit = item.Unit;
                product.IsActive = true;
                product.IsVerified = true;
                result.Add(product);
            }
        }

        await context.SaveChangesAsync();

        var projects = await context.Projects.Where(p => p.IsActive).ToListAsync();
        foreach (var project in projects)
        {
            var existing = await context.ProductProjectMappings.Where(m => m.ProjectId == project.Id).ToListAsync();
            if (existing.Count > 0) continue;

            var slice = result.Skip((projects.IndexOf(project) % 3) * (result.Count / 3)).Take(result.Count / 3 + 1).Take(80).ToList();
            foreach (var code in slice)
            {
                context.ProductProjectMappings.Add(new ProductProjectMapping { ProjectId = project.Id, ProductCodeId = code.Id });
            }
        }

        await context.SaveChangesAsync();
        return result;
    }

    private static async Task SeedEmailTemplatesAsync(AppDbContext context, List<IssueType> issueTypes)
    {
        const string defaultRecipients = "office@iform-aluminium.com";

        var specs = new (string Code, string Name, string IssueTypeCode, string Subject, string Body)[]
        {
            (EmailTemplateCodes.Missing, "Missing Items",
                IssueTypeCodes.Missing,
                "Site query {QUERYNO} - Missing items for {IPO} at {PROJECT}",
                "<p>Dear Sir / Madam,</p>" +
                "<p><b>{SENDER}</b> reported <b>missing stock</b> for project <b>{PROJECT}</b> (IPO <b>{IPO}</b>).</p>" +
                "<p>Product: <b>{PRODUCTCODE}</b><br/>Required qty: <b>{QUANTITYNOS} nos / {QUANTITYSQM} sq.m</b></p>" +
                "<p>{DESCRIPTION}</p>" +
                "<p>Please confirm dispatch availability at the earliest.</p>" +
                "<p>Regards,<br/>IFORM Site Query System</p>"),
            (EmailTemplateCodes.ProductionMistake, "Production Mistake",
                IssueTypeCodes.ProductionMistake,
                "Site query {QUERYNO} - Production mistake at {PROJECT} (IPO {IPO})",
                "<p>Dear Sir / Madam,</p>" +
                "<p><b>{SENDER}</b> reported a <b>production mistake</b> at <b>{PROJECT}</b> (IPO <b>{IPO}</b>).</p>" +
                "<p>Product: <b>{PRODUCTCODE}</b><br/>Quantity affected: <b>{QUANTITYNOS} nos / {QUANTITYSQM} sq.m</b></p>" +
                "<p>{DESCRIPTION}</p>" +
                "<p>Please investigate the batch and coordinate a replacement.</p>" +
                "<p>Regards,<br/>IFORM Site Query System</p>"),
            (EmailTemplateCodes.DesignMistake, "Design Mistake",
                IssueTypeCodes.DesignMistake,
                "Site query {QUERYNO} - Design mismatch at {PROJECT} (IPO {IPO})",
                "<p>Dear Sir / Madam,</p>" +
                "<p><b>{SENDER}</b> flagged a <b>design mistake</b> in project <b>{PROJECT}</b> (IPO <b>{IPO}</b>).</p>" +
                "<p>Product: <b>{PRODUCTCODE}</b></p>" +
                "<p>{DESCRIPTION}</p>" +
                "<p>Kindly review the drawings and advise the revised configuration.</p>" +
                "<p>Regards,<br/>IFORM Site Query System</p>"),
            (EmailTemplateCodes.DispatchMissing, "Dispatch Missing",
                IssueTypeCodes.DispatchMissing,
                "Site query {QUERYNO} - Dispatch missing for {IPO} at {PROJECT}",
                "<p>Dear Sir / Madam,</p>" +
                "<p><b>{SENDER}</b> noted a <b>dispatch discrepancy</b> at <b>{PROJECT}</b> (IPO <b>{IPO}</b>).</p>" +
                "<p>Product: <b>{PRODUCTCODE}</b><br/>Short qty: <b>{QUANTITYNOS} nos / {QUANTITYSQM} sq.m</b></p>" +
                "<p>{DESCRIPTION}</p>" +
                "<p>Please reconcile the dispatch sheet and release the balance.</p>" +
                "<p>Regards,<br/>IFORM Site Query System</p>")
        };

        foreach (var (code, name, issueTypeCode, subject, body) in specs)
        {
            var existing = await context.EmailTemplates.FirstOrDefaultAsync(t => t.Code == code);
            if (existing is null)
            {
                var issueType = issueTypes.FirstOrDefault(i => i.Code == issueTypeCode);
                context.EmailTemplates.Add(new EmailTemplate
                {
                    Code = code,
                    Name = $"Default - {name}",
                    IssueTypeId = issueType?.Id,
                    IsDefault = true,
                    IsActive = true,
                    Subject = subject,
                    Body = body,
                    DefaultRecipients = defaultRecipients
                });
            }
            else if (string.IsNullOrWhiteSpace(existing.DefaultRecipients))
            {
                existing.DefaultRecipients = defaultRecipients;
            }
        }

        await context.SaveChangesAsync();
    }

    private static void SeedQueries(
        AppDbContext context, User manager, User engineer, List<IssueType> issueTypes,
        List<Project> projects, List<ProductCode> productCodes)
    {
        var raisedBy = engineer.Id;
        var managerId = manager.Id;
        var today = DateTimeOffset.UtcNow.Date;

        ProductCode? ByCode(string code) => productCodes.FirstOrDefault(p => p.Code == code);
        IssueType? ByType(string code) => issueTypes.FirstOrDefault(i => i.Code == code);

        var q1 = new Query
        {
            QueryNo = "SQ-0001", IPO = "IPO-2401-001", QuantityNos = 45, QuantitySqm = 67.5m,
            SlabTarget = "Level 3", SlabCompleted = "Level 2",
            RaisedByUserId = raisedBy, IssueTypeId = ByType("MISSING")!.Id, ProjectId = projects[0].Id,
            VerifiedProductCodeId = ByCode("DAAA")?.Id, ProductCodeText = "DAAA",
            DispatchStatus = DispatchStatus.NotDispatched, Status = QueryStatus.Pending,
            Description = "Snap ties (DAAA) not dispatched for Level 3 slab; required before pour day.",
            RaiseDate = today.AddDays(-10), DelayDays = 10, CreatedAt = today.AddDays(-10)
        };
        var q2 = new Query
        {
            QueryNo = "SQ-0002", IPO = "IPO-2401-002", QuantityNos = 120, QuantitySqm = 180m,
            SlabTarget = "Level 5", SlabCompleted = "Level 4",
            RaisedByUserId = raisedBy, IssueTypeId = ByType("MISSING")!.Id, ProjectId = projects[1].Id,
            VerifiedProductCodeId = ByCode("DABA")?.Id, ProductCodeText = "DABA",
            DispatchStatus = DispatchStatus.PartiallyDispatched, Status = QueryStatus.Pending,
            Description = "2-hole reusable ties short by 120 nos. Shipment confirmed partially.",
            RaiseDate = today.AddDays(-5), DelayDays = 5, CreatedAt = today.AddDays(-5)
        };
        var q3 = new Query
        {
            QueryNo = "SQ-0003", IPO = "IPO-2401-003", QuantityNos = 60, QuantitySqm = 90m,
            SlabTarget = "Level 2", SlabCompleted = "Level 1",
            RaisedByUserId = raisedBy, IssueTypeId = ByType("MISSING")!.Id, ProjectId = projects[2].Id,
            VerifiedProductCodeId = ByCode("DRVA0001")?.Id, ProductCodeText = "DRVA0001",
            DispatchStatus = DispatchStatus.NotDispatched, Status = QueryStatus.Pending,
            Description = "Support (V1) props not dispatched; delaying slab shuttering.",
            RaiseDate = today.AddDays(-3), DelayDays = 3, CreatedAt = today.AddDays(-3)
        };
        var q4 = new Query
        {
            QueryNo = "SQ-0004", IPO = "IPO-2401-004", QuantityNos = 24, QuantitySqm = 36m,
            SlabTarget = "Level 4", SlabCompleted = "Level 3",
            RaisedByUserId = raisedBy, IssueTypeId = ByType("MISSING")!.Id, ProjectId = projects[3].Id,
            VerifiedProductCodeId = ByCode("DCAA0001")?.Id, ProductCodeText = "DCAA0001",
            DispatchStatus = DispatchStatus.Dispatched, Status = QueryStatus.InProgress,
            Description = "KK pins delivered but count mismatch; verifying with site store.",
            RaiseDate = today.AddDays(-8), DelayDays = 8, CreatedAt = today.AddDays(-8),
            ResolvedByUserId = managerId
        };
        var q5 = new Query
        {
            QueryNo = "SQ-0005", IPO = "IPO-2401-005", QuantityNos = 8, QuantitySqm = 12m,
            SlabTarget = "Level 6", SlabCompleted = "Level 5",
            RaisedByUserId = raisedBy, IssueTypeId = ByType("PRODUCTION_MISTAKE")!.Id, ProjectId = projects[4].Id,
            VerifiedProductCodeId = ByCode("DCCA0001")?.Id, ProductCodeText = "DCCA0001",
            DispatchStatus = DispatchStatus.Dispatched, Status = QueryStatus.Resolved,
            Description = "Wedge batch with burrs; replaced by quality team.",
            RaiseDate = today.AddDays(-15), DelayDays = 0, CreatedAt = today.AddDays(-15),
            ResolvedByUserId = managerId, ResolvedDate = today.AddDays(-12)
        };
        var q6 = new Query
        {
            QueryNo = "SQ-0006", IPO = "IPO-2401-006", QuantityNos = 30, QuantitySqm = 45m,
            SlabTarget = "Level 3", SlabCompleted = "Level 2",
            RaisedByUserId = raisedBy, IssueTypeId = ByType("DESIGN_MISTAKE")!.Id, ProjectId = projects[0].Id,
            VerifiedProductCodeId = ByCode("DQAA09001100")?.Id, ProductCodeText = "DQAA09001100",
            DispatchStatus = DispatchStatus.Dispatched, Status = QueryStatus.Pending,
            Description = "Door brace layout mismatch with structural drawing at grid 4/B.",
            RaiseDate = today.AddDays(-2), DelayDays = 2, CreatedAt = today.AddDays(-2)
        };
        var q7 = new Query
        {
            QueryNo = "SQ-0007", IPO = "IPO-2401-007", QuantityNos = 15, QuantitySqm = 22.5m,
            SlabTarget = "Level 7", SlabCompleted = "Level 6",
            RaisedByUserId = raisedBy, IssueTypeId = ByType("DISPATCH_MISSING")!.Id, ProjectId = projects[1].Id,
            VerifiedProductCodeId = ByCode("DCBB0100")?.Id, ProductCodeText = "DCBB0100",
            DispatchStatus = DispatchStatus.NotDispatched, Status = QueryStatus.InProgress,
            Description = "Long pins 100L allocated to wrong project in dispatch sheet.",
            RaiseDate = today.AddDays(-6), DelayDays = 6, CreatedAt = today.AddDays(-6),
            ResolvedByUserId = managerId
        };
        var q8 = new Query
        {
            QueryNo = "SQ-0008", IPO = "IPO-2401-008", QuantityNos = 90, QuantitySqm = 135m,
            SlabTarget = "Level 8", SlabCompleted = "Level 7",
            RaisedByUserId = raisedBy, IssueTypeId = ByType("MISSING")!.Id, ProjectId = projects[2].Id,
            VerifiedProductCodeId = ByCode("DLAA0003")?.Id, ProductCodeText = "DLAA0003",
            DispatchStatus = DispatchStatus.PartiallyDispatched, Status = QueryStatus.Pending,
            Description = "PVC pipes 5/8 in transit; delivery ETA needs confirmation.",
            RaiseDate = today.AddDays(-1), DelayDays = 1, CreatedAt = today.AddDays(-1)
        };
        var q9 = new Query
        {
            QueryNo = "SQ-0009", IPO = "IPO-2401-009", QuantityNos = 40, QuantitySqm = 60m,
            SlabTarget = "Level 4", SlabCompleted = "Level 3",
            RaisedByUserId = raisedBy, IssueTypeId = ByType("PRODUCTION_MISTAKE")!.Id, ProjectId = projects[3].Id,
            VerifiedProductCodeId = ByCode("DEAA0600")?.Id, ProductCodeText = "DEAA0600",
            DispatchStatus = DispatchStatus.Dispatched, Status = QueryStatus.Resolved,
            Description = "Wall bracket powder coating flaking; re-coated and re-dispatched.",
            RaiseDate = today.AddDays(-20), DelayDays = 0, CreatedAt = today.AddDays(-20),
            ResolvedByUserId = managerId, ResolvedDate = today.AddDays(-16)
        };
        var q10 = new Query
        {
            QueryNo = "SQ-0010", IPO = "IPO-2401-010", QuantityNos = 12, QuantitySqm = 18m,
            SlabTarget = "Level 5", SlabCompleted = "Level 4",
            RaisedByUserId = raisedBy, IssueTypeId = ByType("DISPATCH_MISSING")!.Id, ProjectId = projects[4].Id,
            VerifiedProductCodeId = ByCode("DEBA1000")?.Id, ProductCodeText = "DEBA1000",
            DispatchStatus = DispatchStatus.NotDispatched, Status = QueryStatus.Pending,
            Description = "Slab brackets not picked for dispatch due to mislabeling in warehouse.",
            RaiseDate = today.AddDays(-4), DelayDays = 4, CreatedAt = today.AddDays(-4)
        };

        context.Queries.AddRange(q1, q2, q3, q4, q5, q6, q7, q8, q9, q10);
        context.QueryStatusHistories.AddRange(
            new QueryStatusHistory { QueryId = q1.Id, FromStatus = QueryStatus.Pending, ToStatus = QueryStatus.Pending, ChangedByUserId = raisedBy, ChangedAt = q1.RaiseDate },
            new QueryStatusHistory { QueryId = q4.Id, FromStatus = QueryStatus.Pending, ToStatus = QueryStatus.InProgress, ChangedByUserId = managerId, ChangedAt = today.AddDays(-6) },
            new QueryStatusHistory { QueryId = q5.Id, FromStatus = QueryStatus.Pending, ToStatus = QueryStatus.InProgress, ChangedByUserId = managerId, ChangedAt = today.AddDays(-14) },
            new QueryStatusHistory { QueryId = q5.Id, FromStatus = QueryStatus.InProgress, ToStatus = QueryStatus.Resolved, ChangedByUserId = managerId, ChangedAt = today.AddDays(-12) },
            new QueryStatusHistory { QueryId = q7.Id, FromStatus = QueryStatus.Pending, ToStatus = QueryStatus.InProgress, ChangedByUserId = managerId, ChangedAt = today.AddDays(-4) },
            new QueryStatusHistory { QueryId = q9.Id, FromStatus = QueryStatus.Pending, ToStatus = QueryStatus.InProgress, ChangedByUserId = managerId, ChangedAt = today.AddDays(-18) },
            new QueryStatusHistory { QueryId = q9.Id, FromStatus = QueryStatus.InProgress, ToStatus = QueryStatus.Resolved, ChangedByUserId = managerId, ChangedAt = today.AddDays(-16) });

        context.QueryComments.AddRange(
            new QueryComment { QueryId = q1.Id, UserId = raisedBy, CommentText = "Attached site photo of staging area showing empty rack.", CreatedAt = today.AddDays(-9) },
            new QueryComment { QueryId = q1.Id, UserId = managerId, CommentText = "Coordinating with dispatch; expect release by tomorrow.", CreatedAt = today.AddDays(-8) },
            new QueryComment { QueryId = q4.Id, UserId = raisedBy, CommentText = "Received revised packing list; recount in progress.", CreatedAt = today.AddDays(-7) },
            new QueryComment { QueryId = q6.Id, UserId = managerId, CommentText = "Design team notified; updated door brace layout attached.", CreatedAt = today.AddDays(-1) },
            new QueryComment { QueryId = q8.Id, UserId = raisedBy, CommentText = "Transporter updated ETA to next morning.", CreatedAt = today.AddDays(0) });
    }
}