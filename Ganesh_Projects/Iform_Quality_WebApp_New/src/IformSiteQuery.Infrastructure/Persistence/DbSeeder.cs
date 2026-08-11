using IformSiteQuery.Domain.Entities;
using IformSiteQuery.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IformSiteQuery.Infrastructure.Persistence;

public static class DbSeeder
{
    public static async Task InitializeAsync(AppDbContext db, ILogger logger)
    {
        await db.Database.MigrateAsync();
        await SeedAsync(db, logger);
    }

    public static async Task SeedAsync(AppDbContext db, ILogger? logger = null)
    {
        if (await db.Users.AnyAsync())
            return;

        var hasher = new PasswordHasher<User>();
        var now = DateTime.UtcNow;

        // ---------- Projects (from the live tracker) ----------
        var projects = new List<Project>
        {
            new() { Name = "Hallmark", Client = "Hallmark Developers", Location = "Hyderabad" },
            new() { Name = "SRR Khammam", Client = "SRR Group", Location = "Khammam" },
            new() { Name = "Golkonda Tattvam", Client = "Golkonda Infra", Location = "Hyderabad" },
            new() { Name = "Reliance E-1", Client = "Reliance Ventures", Location = "Mumbai" },
            new() { Name = "North Star", Client = "North Star Projects", Location = "Bengaluru" },
            new() { Name = "Olympus-2 Hilite-A", Client = "Hilite Group", Location = "Calicut" },
            new() { Name = "Profound Vanam Tower-1", Client = "Profound Estates", Location = "Hyderabad" },
            new() { Name = "Techno Paints One Nine", Client = "Techno Group", Location = "Vijayawada" },
            new() { Name = "Siddhartha Academic", Client = "Siddhartha Group", Location = "Guntur" },
            new() { Name = "Olympus-2 Hilite-B", Client = "Hilite Group", Location = "Calicut" }
        };
        db.Projects.AddRange(projects);
        await db.SaveChangesAsync();

        // ---------- Users ----------
        var manager = new User
        {
            FullName = "Venkatesh K",
            Email = "manager@iform.co.in",
            Phone = "9000000001",
            Role = UserRole.Manager,
            IsActive = true,
            CreatedAt = now
        };
        manager.PasswordHash = hasher.HashPassword(manager, "Manager@123");

        var siteEngineer = new User
        {
            FullName = "Ramesh Kumar",
            Email = "engineer@iform.co.in",
            Phone = "9000000002",
            Role = UserRole.SiteEngineer,
            IsActive = true,
            ProjectId = projects[0].Id,
            CreatedAt = now
        };
        siteEngineer.PasswordHash = hasher.HashPassword(siteEngineer, "Engineer@123");

        var engineer2 = new User
        {
            FullName = "Srinivas Rao",
            Email = "srinivas@iform.co.in",
            Phone = "9000000003",
            Role = UserRole.SiteEngineer,
            IsActive = true,
            ProjectId = projects[3].Id,
            CreatedAt = now
        };
        engineer2.PasswordHash = hasher.HashPassword(engineer2, "Engineer@123");

        db.Users.AddRange(manager, siteEngineer, engineer2);
        await db.SaveChangesAsync();

        // ---------- Product catalogue (from Accessories with Photos) ----------
        var products = BuildProductCatalogue();
        db.Products.AddRange(products);
        await db.SaveChangesAsync();

        var pin = products.First(p => p.Code == "DCAA0001");
        var wedge = products.First(p => p.Code == "DCCA0001");
        var wallBracket = products.First(p => p.Code == "DEAA0600");
        var tieRod = products.First(p => p.Code == "DAGB");
        var snapTie = products.First(p => p.Code == "DAAA");

        // ---------- Sample queries (mirrors tracker sample rows) ----------
        var queries = new List<SiteQuery>
        {
            new() { IpoNumber = "561", ProjectId = projects[3].Id, IssueType = IssueType.Missing, QtyNos = 12, QtySqm = 3.06m, Description = "Tie rods and snap ties missing from dispatch for Tower E-1 slab work.", ProductId = tieRod.Id, Status = QueryStatus.Pending, RaisedById = siteEngineer.Id, RaisedAt = now.AddDays(-61).Date.AddHours(9) },
            new() { IpoNumber = "565", ProjectId = projects[2].Id, IssueType = IssueType.Missing, QtyNos = 8, QtySqm = 2.4m, Description = "Missing wall brackets at Golkonda Tattvam, lift core area.", ProductId = wallBracket.Id, Status = QueryStatus.InProgress, RaisedById = engineer2.Id, RaisedAt = now.AddDays(-46).Date.AddHours(10) },
            new() { IpoNumber = "571", ProjectId = projects[1].Id, IssueType = IssueType.Missing, QtyNos = 20, QtySqm = 0, Description = "Snap ties shortfall for SRR Khammam level 6.", ProductId = snapTie.Id, Status = QueryStatus.Pending, RaisedById = siteEngineer.Id, RaisedAt = now.AddDays(-46).Date.AddHours(11) },
            new() { IpoNumber = "556", ProjectId = projects[0].Id, IssueType = IssueType.Missing, QtyNos = 5, QtySqm = 1.5m, Description = "Form clips missing in Hallmark package 4.", ProductId = null, Status = QueryStatus.Pending, RaisedById = siteEngineer.Id, RaisedAt = now.AddDays(-45).Date.AddHours(12) },
            new() { IpoNumber = "535", ProjectId = projects[4].Id, IssueType = IssueType.Missing, QtyNos = 15, QtySqm = 0, Description = "Missing wedges for North Star shear wall panels.", ProductId = wedge.Id, Status = QueryStatus.InProgress, RaisedById = engineer2.Id, RaisedAt = now.AddDays(-32).Date.AddHours(8) },
            new() { IpoNumber = "580", ProjectId = projects[5].Id, IssueType = IssueType.DispatchMissing, QtyNos = 15, QtySqm = 0, Description = "Dispatch missing items not received for Olympus-2 Hilite-A slab.", ProductId = null, Status = QueryStatus.Pending, RaisedById = siteEngineer.Id, RaisedAt = now.AddDays(-26).Date.AddHours(9) },
            new() { IpoNumber = "581", ProjectId = projects[5].Id, IssueType = IssueType.DesignMistake, QtyNos = 4, QtySqm = 1.2m, Description = "Beam pocket position wrong per latest structural revision.", ProductId = null, Status = QueryStatus.InProgress, RaisedById = siteEngineer.Id, RaisedAt = now.AddDays(-26).Date.AddHours(10) },
            new() { IpoNumber = "582", ProjectId = projects[5].Id, IssueType = IssueType.ProductionMistake, QtyNos = 3, QtySqm = 0.9m, Description = "Production mistake on edge beams - hole positions misaligned.", ProductId = null, Status = QueryStatus.Resolved, RaisedById = engineer2.Id, RaisedAt = now.AddDays(-26).Date.AddHours(11), ResolvedById = manager.Id, ResolvedAt = now.AddDays(-20).Date.AddHours(15), ResolutionNote = "Replacement panels dispatched on 22/07/2026." },
            new() { IpoNumber = "588", ProjectId = projects[6].Id, IssueType = IssueType.ProductionMistake, QtyNos = 6, QtySqm = 2.1m, Description = "Production mistake on column shutters at Vanam Tower-1.", ProductId = null, Status = QueryStatus.Pending, RaisedById = siteEngineer.Id, RaisedAt = now.AddDays(-24).Date.AddHours(9) },
            new() { IpoNumber = "589", ProjectId = projects[6].Id, IssueType = IssueType.DispatchMissing, QtyNos = 10, QtySqm = 0, Description = "Dispatch missing - bracing set short at site.", ProductId = null, Status = QueryStatus.InProgress, RaisedById = engineer2.Id, RaisedAt = now.AddDays(-24).Date.AddHours(10) },
            new() { IpoNumber = "595", ProjectId = projects[7].Id, IssueType = IssueType.DispatchMissing, QtyNos = 9, QtySqm = 0, Description = "Dispatch missing items at Techno Paints One Nine.", ProductId = null, Status = QueryStatus.Pending, RaisedById = siteEngineer.Id, RaisedAt = now.AddDays(-17).Date.AddHours(11) },
            new() { IpoNumber = "596", ProjectId = projects[8].Id, IssueType = IssueType.DispatchMissing, QtyNos = 1, QtySqm = 23, Description = "Dispatch missing item - scaffold plank set for Siddhartha Academic.", ProductId = null, Status = QueryStatus.Pending, RaisedById = engineer2.Id, RaisedAt = now.AddDays(-17).Date.AddHours(12) },
            new() { IpoNumber = "600", ProjectId = projects[9].Id, IssueType = IssueType.Missing, QtyNos = 2, QtySqm = 0, Description = "Missing panel join bolts for Hilite-B first pour.", ProductId = null, Status = QueryStatus.Resolved, RaisedById = siteEngineer.Id, RaisedAt = now.AddDays(-12).Date.AddHours(9), ResolvedById = manager.Id, ResolvedAt = now.AddDays(-8).Date.AddHours(14), ResolutionNote = "Bolts dispatched on 03/08/2026." },
            new() { IpoNumber = "601", ProjectId = projects[8].Id, IssueType = IssueType.DesignMistake, QtyNos = 2, QtySqm = 0.6m, Description = "Design mistake in staircase landing profile for Siddhartha block B.", ProductId = null, Status = QueryStatus.Pending, RaisedById = siteEngineer.Id, RaisedAt = now.AddDays(-8).Date.AddHours(10) }
        };
        // Assign sequential query numbers
        var seq = 1;
        foreach (var q in queries)
        {
            q.QueryNumber = $"QRY-{now.Year}-{seq++:D4}";
        }

        db.Queries.AddRange(queries);
        await db.SaveChangesAsync();

        // ---------- Comments on open queries ----------
        var qReliance = queries[0];
        var qGolkonda = queries[1];
        db.QueryComments.AddRange(
            new QueryComment { QueryId = qReliance.Id, UserId = siteEngineer.Id, Text = "Site is ready to cast; awaiting material for last 3 weeks.", CreatedAt = now.AddDays(-30).Date.AddHours(9) },
            new QueryComment { QueryId = qReliance.Id, UserId = manager.Id, Text = "Dispatch scheduled, checking with production team.", CreatedAt = now.AddDays(-20).Date.AddHours(16) },
            new QueryComment { QueryId = qGolkonda.Id, UserId = engineer2.Id, Text = "Re-verified at site, still 6 brackets outstanding.", CreatedAt = now.AddDays(-10).Date.AddHours(9) }
        );
        await db.SaveChangesAsync();

        // ---------- Audit log entries ----------
        db.AuditLogs.AddRange(
            new AuditLog { UserId = siteEngineer.Id, UserName = siteEngineer.FullName, Action = "QueryRaised", EntityType = "SiteQuery", EntityId = queries[0].QueryNumber, Details = $"IPO {queries[0].IpoNumber} - {IssueTypeDisplay(queries[0].IssueType)}", Timestamp = queries[0].RaisedAt },
            new AuditLog { UserId = manager.Id, UserName = manager.FullName, Action = "QueryResolved", EntityType = "SiteQuery", EntityId = queries[7].QueryNumber, Details = $"IPO {queries[7].IpoNumber} marked Resolved", Timestamp = queries[7].ResolvedAt!.Value },
            new AuditLog { UserId = manager.Id, UserName = manager.FullName, Action = "QueryResolved", EntityType = "SiteQuery", EntityId = queries[12].QueryNumber, Details = $"IPO {queries[12].IpoNumber} marked Resolved", Timestamp = queries[12].ResolvedAt!.Value }
        );
        await db.SaveChangesAsync();

        logger?.LogInformation("Database seeded: {users} users, {projects} projects, {products} products, {queries} queries.",
            db.Users.Count(), db.Projects.Count(), db.Products.Count(), db.Queries.Count());
    }

    private static string IssueTypeDisplay(IssueType type) => type switch
    {
        IssueType.Missing => "Missing",
        IssueType.ProductionMistake => "Production Mistake",
        IssueType.DesignMistake => "Design Mistake",
        _ => "Dispatch Missing"
    };

    private static List<Product> BuildProductCatalogue()
    {
        var list = new List<(string Code, string Name, string Category, string Spec, string Material)>
        {
            // Snap / reusable ties
            ("DAAA", "Snap Tie", "Snap Tie", "Wall thickness (mm)", "Steel"),
            ("DABA", "2-Hole Reusable Tie", "Reusable Tie", "Wall thickness (mm)", "Steel"),
            ("DACA", "3-Hole Reusable Tie (W37)", "Reusable Tie", "Wall thickness (mm)", "Steel"),
            ("DAHA", "3-Hole Reusable Tie (W33)", "Reusable Tie", "Wall thickness (mm)", "Steel"),
            ("DTGD", "Re-Cone Tie", "Tie", "[1/2] - Wall thickness (mm)", "Steel + PVC"),
            ("DADA", "T-Tie", "Tie", "Wall thickness (mm)", "Steel"),
            ("DAFA", "Double Pour Tie", "Tie", "Wall th'k - Wall space distance", "Steel"),
            ("DAGA", "AL-Rod Tie", "Tie", "Wall thickness (mm)", "Steel"),
            ("DAGB", "Tie Rod (1/2)", "Tie", "Length", "Steel"),
            ("DAGC", "Tie Rod (5/8)", "Tie", "Length", "Steel"),
            ("DAIB", "Sepa Bolt (1/2)", "Bolt", "- Wall thickness (mm)", "Steel"),
            ("DAIC", "Sepa Bolt (5/8)", "Bolt", "- Wall thickness (mm)", "Steel"),
            // Supports & cones
            ("DRVA0001", "Support (V1)", "Prop Support", "Min. - Max. Length", "Steel"),
            ("DRVA0002", "Support (V2)", "Prop Support", "Min. - Max. Length", "Steel"),
            ("DRWA0001", "Support (V3)", "Prop Support", "Min. - Max. Length", "Steel"),
            ("DRWA0002", "Support (V4)", "Prop Support", "Min. - Max. Length", "Steel"),
            ("DRTA0005", "Pipe Head Adaptor", "Prop Support", "Pipe Dia.", "Steel"),
            ("DBAA0000", "D-Cone [1/2] - 40MM", "Cone", "[1/2] 40MM", "Steel + PVC"),
            ("DBAA0000", "D-Cone [5/8] - 60MM", "Cone", "[5/8] 60MM", "Steel + PVC"),
            // Pins & wedges
            ("DCAA0001", "Pin (KK-Type)", "Pin", "KK", "Steel"),
            ("DCAA0015", "Pin (ALFA-Type)", "Pin", "ASIA", "Steel"),
            ("DCAB0059", "Pin (AO-Type)", "Pin", "A-ONE", "Steel"),
            ("DCAC0059", "Pin (ALFU-Type)", "Pin", "USA", "Steel"),
            ("DCBA0064", "Long Pin 64L", "Long Pin", "ALF - Form Clip", "Steel"),
            ("DCBB0100", "Long Pin 100L", "Long Pin", "HD - 100L", "Steel"),
            ("DCBB0150", "Long Pin 150L", "Long Pin", "SM - 150L", "Steel"),
            ("DCBB0152", "Long Pin 152L", "Long Pin", "KK - 152L", "Steel"),
            ("DCBC0157", "Long Pin 157L", "Long Pin", "ALF - Pin", "Steel"),
            ("DCCA0001", "Wedge (ALFA-Type)", "Wedge", "ASIA", "Steel"),
            ("DCCB0001", "Wedge (AO-Type)", "Wedge", "A-ONE", "Steel"),
            ("DCCC0001", "Straight Wedge (ALFU-Type)", "Wedge", "USA", "Steel"),
            ("DCCD0001", "5 Degree Curved Wedge (ALFU-Type)", "Wedge", "USA", "Steel"),
            ("DCCE0001", "Curved Wedge (ALFU-Type)", "Wedge", "USA", "Steel"),
            // Brackets
            ("DDAA0001", "Adjustable Waler Bracket (ALFA-Type)", "Waler Bracket", "50x50", "Steel"),
            ("DDAA0003", "Adjustable Waler Bracket (ALFU-Type)", "Waler Bracket", "2x4", "Steel"),
            ("DDBA0001", "STD. Waler (ALFU-Type)", "Waler Bracket", "2x4", "Steel"),
            ("DRMA", "Waler Board 50x50x3.2t", "Waler Board", "Length (M)", "Steel"),
            ("DDCA0099", "KL Bracket \"U\" Type - 99.2MM", "KL Bracket", "U-99.2MM", "Steel"),
            ("DDCB0099", "KL Bracket \"Z\" Type - 99.2MM", "KL Bracket", "Z-99.2MM", "Steel"),
            ("DDCE0092", "KL Bracket \"U\" Type - 92.5MM", "KL Bracket", "U-92.5MM", "Steel"),
            ("DDCF0092", "KL Bracket \"Z\" Type - 92.5MM", "KL Bracket", "Z-92.5MM", "Steel"),
            ("DEAA0600", "STD. Wall Bracket (Dywidag-Type)", "Wall Bracket", "1150X1000X600", "Steel"),
            ("DEAA0740", "Wall Bracket (TIE-Type)", "Wall Bracket", "1070X950X740", "Steel"),
            ("DEBA1000", "Slab Bracket", "Wall Bracket", "1150X1000", "Steel"),
            ("DECA0245", "Special Wall Bracket", "Wall Bracket", "1150X1000X245", "Steel"),
            ("DFAA", "Bracket Bolt", "Bracket Bolt", "17 x Length", "Steel"),
            // Anchors & bolts
            ("DFAB1600", "Kicker Anchor Nut", "Kicker Anchor", "M16 x 2.0", "Steel"),
            ("DFAB1601", "Kicker Anchor Washer", "Kicker Anchor", "M16", "Steel"),
            ("DFAB1610", "Anchor Sleeve 100MM", "Kicker Anchor", "100MM", "PVC"),
            ("DFAB1675", "Kicker Anchor Bolt", "Kicker Anchor", "M16x75L", "Steel"),
            ("DFAC1610", "Dywidag Kicker Anchor Bolt", "Kicker Anchor", "100mm", "Steel"),
            ("DFAC1611", "Dywidag Kicker Anchor AL-Nut", "Kicker Anchor", "M16", "Aluminum"),
            ("DFAC1635", "Panel Join - Bolt", "Hex Bolt", "M16x35", "Steel"),
            ("DFAC1636", "Panel Join - Nut", "Hex Bolt", "M16", "Steel"),
            ("DFAE", "Dywidag Bolt", "Dywidag Bolt", "17 x Length", "Steel"),
            ("DFAF0150", "Waler Fixing Bolt (Hex Bolt-Type)", "Waler Fixing Bolt", "M16*35 - Length", "Steel"),
            ("DFAG0200", "Waler Fixing Bolt (Pin-Type) - 5/8", "Waler Fixing Bolt", "Length", "Steel"),
            ("DFAH2012", "Waler Fixing Bolt (Pin-Type) - 1/2", "Waler Fixing Bolt", "Length", "Steel"),
            // Nuts, clips, pin locks
            ("DHAA0001", "Wing Nut 1/2", "Wing Nut", "1/2\"", "Cast-iron"),
            ("DHBA0001", "Wing Nut 5/8", "Wing Nut", "5/8\"", "Cast-iron"),
            ("DIAA0001", "Form Clip-LH (ALFA-Type)", "Form Clip", "LH (Asia)", "Steel"),
            ("DIAB0001", "Form Clip-RH (ALFA-Type)", "Form Clip", "RH (Asia)", "Steel"),
            ("DIBA0001", "Form Clip-LH (ALFU-Type)", "Form Clip", "LH (USA)", "Steel"),
            ("DIBB0001", "Form Clip-RH (ALFU-Type)", "Form Clip", "RH (USA)", "Steel"),
            ("DJAC0001", "Pin Lock PVC Cylinder", "Pin Lock", "PVC", "PVC"),
            ("DJBA0001", "Pin Lock LH-16.5 (Wall)", "Pin Lock", "LH (Asia)", "Steel + PVC"),
            ("DJBB0001", "Pin Lock RH-16.5 (Wall)", "Pin Lock", "RH (Asia)", "Steel + PVC"),
            // PVC & pipes
            ("DKAA", "PVC Tie Sleeve", "PVC", "Wall thickness (mm)", "PVC"),
            ("DLAA0000", "PVC Pipe 22, 2M", "PVC Pipe", "22 / 2M", "PVC"),
            ("DLAA0002", "PVC Pipe [1/2, 2M]", "PVC Pipe", "[1/2 - 2M]", "PVC"),
            ("DLAA0003", "PVC Pipe [5/8, 2M]", "PVC Pipe", "[5/8 - 2M]", "PVC"),
            // Braces
            ("DQAA04000900", "Door Brace 400~900", "Door Brace", "400~900", "Steel"),
            ("DQAA05000700", "Door Brace 500~700", "Door Brace", "600", "Steel"),
            ("DQAA06000800", "Door Brace 600~800", "Door Brace", "600~800", "Steel"),
            ("DQAA07000900", "Door Brace 700~900", "Door Brace", "700~900", "Steel"),
            ("DQAA07001100", "Door Brace 700~1100", "Door Brace", "700~1100", "Steel"),
            ("DQAA07500950", "Door Brace 750~950", "Door Brace", "750~950", "Steel"),
            ("DQAA09001100", "Door Brace 900~1100", "Door Brace", "900~1100", "Steel"),
            ("DQAA09001600", "Door Brace 900~1600", "Door Brace", "900~1600", "Steel"),
            ("DQAA09501100", "Door Brace 950~1100", "Door Brace", "950~1100", "Steel"),
            ("DQAA10501200", "Door Brace 1050~1200", "Door Brace", "1050~1200", "Steel"),
            ("DQAA11001300", "Door Brace 1100~1300", "Door Brace", "1100~1300", "Steel"),
            ("DQAA11501300", "Door Brace 1150~1300", "Door Brace", "1150~1300", "Steel"),
            ("DQAA12001400", "Door Brace 1200~1400", "Door Brace", "1200~1400", "Steel"),
            ("DQAA14001600", "Door Brace 1400~1600", "Door Brace", "1400~1600", "Steel"),
            ("DQAA16001800", "Door Brace 1600~1800", "Door Brace", "1600~1800", "Steel"),
            ("DQAA18002000", "Door Brace 1800~2000", "Door Brace", "1800~2000", "Steel"),
            ("DEDA0001", "Low Control Brace", "Door Brace", "600L", "Steel"),
            ("DQAE2000", "Plumbing Wall Brace", "Plumbing Wall Brace", "2000 [2400H]", "Steel"),
            ("DQAE2200", "Plumbing Wall Brace", "Plumbing Wall Brace", "2200 [3000H]", "Steel"),
            ("DQAE2700", "Plumbing Wall Brace", "Plumbing Wall Brace", "2700 [3500H]", "Steel"),
            ("DQAE2800", "Plumbing Wall Brace", "Plumbing Wall Brace", "2800 [3500H]", "Steel"),
            ("DQAG3000", "Plumbing Wall Brace", "Plumbing Wall Brace", "3000", "Steel"),
            ("DZAA", "Push-Pull Bracing Set", "Bracing", "Long 1800L & Short 800L", "Steel"),
            ("DQAB0001", "Cap Braces (ALFU-Type)", "Bracing", "STD (USA)", "Steel"),
            ("DQAB0700", "Cap Braces (Special)", "Bracing", "Special (700)", "Steel"),
            ("DQAF0600", "Cap Braces (ALFA-Type)", "Bracing", "STD (Asia)", "Steel"),
            // Tools & misc
            ("DPAA0001", "Tie Keeper (Omniwedge)", "Tools", "Omniwedge", "Steel"),
            ("DRAA1710", "Bracket Flange Nut", "Tools", "17-100", "Cast-iron"),
            ("DRBA0001", "Tie Puller", "Tools", "Standard", "Steel"),
            ("DRAA0001", "Pin Lock Stripping Tool", "Tools", "Standard", "Cast-iron"),
            ("DRCA0002", "Panel Puller", "Tools", "Y style (Panel Puller)", "Steel"),
            ("DRDA0001", "Hole Aligner", "Tools", "Standard", "Steel"),
            ("DRFA0001", "Tie Breaker Bar", "Tools", "Standard", "Steel"),
            ("DRGA0001", "Sleeve Eject Bar", "Tools", "Standard", "Steel"),
            ("DRNA0002", "Work Bench (1000H)", "Tools", "1200x500x1000 (H)", "Steel"),
            ("DRNA0004", "Work Bench (750H)", "Tools", "1200x500x750 (H)", "Steel"),
            ("DROB0001", "Wire Turnbuckle", "Tools", "5/8*6M", "Steel"),
            ("DTGA0001", "PVC Cone", "Misc", "Standard", "PVC"),
            ("DUAA0001", "Square Washer", "Misc", "Standard", "Steel"),
            ("DZAA0004", "Double Waler Nut Clamp", "Misc", "Standard", "Steel"),
            ("DZAA0005", "Double Waler Clamp Washer", "Misc", "130X50", "Steel"),
            ("DZAA0006", "Plastic Cap 16", "Misc", "16", "PVC"),
            ("DZAA0008", "Plastic Cap 18", "Misc", "18", "PVC")
        };

        return list.Select(x => new Product
        {
            Code = x.Code,
            Name = x.Name,
            Category = x.Category,
            Spec = x.Spec,
            Material = x.Material
        }).ToList();
    }
}
