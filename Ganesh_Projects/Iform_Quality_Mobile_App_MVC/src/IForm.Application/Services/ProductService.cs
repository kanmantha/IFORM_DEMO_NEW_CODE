using IForm.Application.Common;
using IForm.Application.Common.Interfaces;
using IForm.Application.DTOs;
using IForm.Contracts;
using IForm.Domain.Entities;
using IForm.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace IForm.Application.Services;

public interface IProductService
{
    Task<PagedResult<ProductListItemDto>> SearchAsync(string? term, Guid? categoryId, int page, int pageSize, CancellationToken ct = default);
    Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Guid> CreateAsync(CreateProductRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken ct = default);
    Task DeactivateAsync(Guid id, CancellationToken ct = default);
    Task<ProductImportResult> ImportCatalogueAsync(IEnumerable<ProductImportRow> rows, CancellationToken ct = default);
    Task<IReadOnlyList<ProductListItemDto>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ProductCategoryDto>> GetCategoriesAsync(CancellationToken ct = default);
    Task<Guid> EnsureCategoryAsync(string name, CancellationToken ct = default);
    Task<int> SeedDefaultCatalogueAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ProductListItemDto>> SearchForProjectAsync(Guid projectId, string? term, CancellationToken ct = default);
}

/// <summary>Portable import row produced by the Excel/CSV/JSON importer.</summary>
public record ProductImportRow(string Code, string Name, string? Category, string? Specification, string? Material, string? Unit, string? Description);

public class ProductService : IProductService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _audit;

    public ProductService(IApplicationDbContext db, ICurrentUser currentUser, IAuditLogger audit)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
    }

    private Guid Tenant => _currentUser.TenantId ?? throw new AuthorizationException("Tenant context is missing.");

    public async Task<PagedResult<ProductListItemDto>> SearchAsync(string? term, Guid? categoryId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.Products
            .Where(x => x.TenantId == Tenant && !x.IsDeleted)
            .Include(x => x.Category)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(term))
        {
            var t = term.Trim();
            query = query.Where(x => x.ProductCode.Contains(t) || x.ProductName.Contains(t) ||
                (x.Specification != null && x.Specification.Contains(t)) || (x.Material != null && x.Material.Contains(t)));
        }
        if (categoryId.HasValue) query = query.Where(x => x.CategoryId == categoryId.Value);

        var list = await query
            .OrderBy(x => x.ProductCode)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        var total = await query.CountAsync(ct);

        var items = list.Select(x => new ProductListItemDto(x.Id, x.ProductCode, x.ProductName, x.Category?.Name,
            x.Specification, x.Material, x.Unit, x.IsActive, x.PhotoPath)).ToList();

        return new PagedResult<ProductListItemDto>(items, total, page, pageSize, (int)Math.Ceiling(total / (double)Math.Max(1, pageSize)));
    }

    public async Task<IReadOnlyList<ProductListItemDto>> GetAllAsync(CancellationToken ct = default)
    {
        var list = await _db.Products
            .Where(x => x.TenantId == Tenant && !x.IsDeleted)
            .Include(x => x.Category)
            .OrderBy(x => x.ProductCode)
            .AsNoTracking()
            .ToListAsync(ct);

        return list.Select(x => new ProductListItemDto(x.Id, x.ProductCode, x.ProductName, x.Category?.Name,
            x.Specification, x.Material, x.Unit, x.IsActive, x.PhotoPath)).ToList();
    }

    public async Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var product = await _db.Products
            .Include(x => x.Category)
            .Include(x => x.ProjectMappings)
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == Tenant, ct);

        return product is null
            ? null
            : new ProductDto(product.Id, product.ProductCode, product.ProductName, product.Description, product.Specification,
                product.Material, product.Unit, product.CategoryId, product.Category?.Name, product.PhotoPath, product.IsActive,
                product.ProjectMappings.Select(m => m.ProjectId).ToList());
    }

    public async Task<Guid> CreateAsync(CreateProductRequest request, CancellationToken ct = default)
    {
        var product = new Product
        {
            TenantId = Tenant,
            ProductCode = request.ProductCode.Trim(),
            ProductName = request.ProductName.Trim(),
            Description = request.Description,
            Specification = request.Specification,
            Material = request.Material,
            Unit = request.Unit,
            CategoryId = request.CategoryId,
            IsActive = request.IsActive,
            Source = "manual"
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync(ct);
        await SetMappingsAsync(product.Id, request.ProjectIds, ct);
        await _audit.LogAsync("Product Created", nameof(Product), product.Id.ToString(), null, product.ProductCode, ct);
        return product.Id;
    }

    public async Task UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken ct = default)
    {
        var product = await _db.Products.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == Tenant, ct)
            ?? throw new NotFoundException("Product not found.");

        product.ProductCode = request.ProductCode.Trim();
        product.ProductName = request.ProductName.Trim();
        product.Description = request.Description;
        product.Specification = request.Specification;
        product.Material = request.Material;
        product.Unit = request.Unit;
        product.CategoryId = request.CategoryId;
        product.IsActive = request.IsActive;
        product.UpdatedAt = DateTime.UtcNow;
        product.UpdatedBy = _currentUser.UserName;

        await _db.SaveChangesAsync(ct);
        await SetMappingsAsync(id, request.ProjectIds, ct);
        await _audit.LogAsync("Product Updated", nameof(Product), id.ToString(), null, product.ProductCode, ct);
    }

    public async Task DeactivateAsync(Guid id, CancellationToken ct = default)
    {
        var product = await _db.Products.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == Tenant, ct)
            ?? throw new NotFoundException("Product not found.");

        product.IsActive = false;
        product.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Product Deactivated", nameof(Product), id.ToString(), null, product.ProductCode, ct);
    }

    public async Task<ProductImportResult> ImportCatalogueAsync(IEnumerable<ProductImportRow> rows, CancellationToken ct = default)
    {
        var tenantId = Tenant;
        var rowList = rows.ToList();
        var messages = new List<string>();
        var imported = 0;
        var duplicates = 0;
        var invalid = 0;
        var warnings = 0;
        var errors = 0;

        var existingCodes = await _db.Products
            .Where(x => x.TenantId == tenantId)
            .Select(x => x.ProductCode)
            .ToHashSetAsync(ct);

        foreach (var row in rowList)
        {
            if (string.IsNullOrWhiteSpace(row.Code) || string.IsNullOrWhiteSpace(row.Name))
            {
                invalid++;
                messages.Add($"INVALID: row missing code or name ({row.Code ?? "-"} / {row.Name ?? "-"}).");
                continue;
            }

            var code = row.Code.Trim();
            if (existingCodes.Contains(code))
            {
                duplicates++;
                continue;
            }

            Guid? categoryId = null;
            if (!string.IsNullOrWhiteSpace(row.Category))
            {
                categoryId = await EnsureCategoryAsync(row.Category, ct);
            }

            _db.Products.Add(new Product
            {
                TenantId = tenantId,
                ProductCode = code,
                ProductName = row.Name.Trim(),
                Specification = row.Specification,
                Material = row.Material,
                Unit = row.Unit,
                Description = row.Description,
                CategoryId = categoryId,
                Source = "import"
            });
            existingCodes.Add(code);
            imported++;
        }

        await _db.SaveChangesAsync(ct);

        if (imported > 0)
            await _audit.LogAsync("Product Catalogue Imported", nameof(Product), null, null, $"{imported} products", ct);

        return new ProductImportResult(rowList.Count, imported, duplicates, invalid, warnings, errors, messages, null);
    }

    public async Task<int> SeedDefaultCatalogueAsync(CancellationToken ct = default)
    {
        var tenantId = Tenant;
        var existing = await _db.Products.Where(x => x.TenantId == tenantId).Select(x => x.ProductCode).ToHashSetAsync(ct);
        var seeded = 0;

        foreach (var item in AccessoryCatalogue.All)
        {
            if (existing.Contains(item.Code)) continue;

            Guid? categoryId = null;
            if (!string.IsNullOrWhiteSpace(item.Category))
                categoryId = await EnsureCategoryAsync(item.Category, ct);

            _db.Products.Add(new Product
            {
                TenantId = tenantId,
                ProductCode = item.Code,
                ProductName = item.Name,
                Specification = item.Specification,
                Material = item.Material,
                Unit = item.Unit,
                CategoryId = categoryId,
                Source = "iform-catalogue"
            });
            existing.Add(item.Code);
            seeded++;
        }

        if (seeded > 0) await _db.SaveChangesAsync(ct);
        return seeded;
    }

    public async Task<IReadOnlyList<ProductCategoryDto>> GetCategoriesAsync(CancellationToken ct = default)
    {
        var list = await _db.ProductCategories
            .Where(x => x.TenantId == Tenant && !x.IsDeleted)
            .Include(x => x.Products.Where(p => !p.IsDeleted))
            .OrderBy(x => x.Name)
            .AsNoTracking()
            .ToListAsync(ct);

        return list.Select(x => new ProductCategoryDto(x.Id, x.Name, x.Description, x.Products.Count)).ToList();
    }

    public async Task<Guid> EnsureCategoryAsync(string name, CancellationToken ct = default)
    {
        var trimmed = name.Trim();
        var existing = await _db.ProductCategories
            .FirstOrDefaultAsync(x => x.TenantId == Tenant && x.Name == trimmed, ct);

        if (existing != null) return existing.Id;

        var category = new ProductCategory { TenantId = Tenant, Name = trimmed };
        _db.ProductCategories.Add(category);
        await _db.SaveChangesAsync(ct);
        return category.Id;
    }

    public async Task<IReadOnlyList<ProductListItemDto>> SearchForProjectAsync(Guid projectId, string? term, CancellationToken ct = default)
    {
        var query = _db.ProductProjectMappings
            .Where(m => m.TenantId == Tenant && m.ProjectId == projectId)
            .Include(m => m.Product)
            .Select(m => m.Product)
            .Where(p => p != null && p.IsActive && !p.IsDeleted);

        if (!string.IsNullOrWhiteSpace(term))
        {
            var t = term.Trim();
            query = query.Where(p => p.ProductCode.Contains(t) || p.ProductName.Contains(t));
        }

        var list = await query.OrderBy(p => p.ProductCode).Take(50).ToListAsync(ct);
        return list.Select(x => new ProductListItemDto(x!.Id, x.ProductCode, x.ProductName, x.Category?.Name,
            x.Specification, x.Material, x.Unit, x.IsActive, x.PhotoPath)).ToList();
    }

    private async Task SetMappingsAsync(Guid productId, IReadOnlyList<Guid> projectIds, CancellationToken ct)
    {
        var existing = await _db.ProductProjectMappings
            .Where(m => m.TenantId == Tenant && m.ProductId == productId)
            .ToListAsync(ct);

        var keep = new HashSet<Guid>(projectIds);
        _db.ProductProjectMappings.RemoveRange(existing.Where(m => !keep.Contains(m.ProjectId)));

        var currentProjectIds = existing.Select(m => m.ProjectId).ToHashSet();
        foreach (var projectId in keep)
        {
            if (currentProjectIds.Contains(projectId)) continue;
            _db.ProductProjectMappings.Add(new ProductProjectMapping
            {
                TenantId = Tenant,
                ProductId = productId,
                ProjectId = projectId
            });
        }

        await _db.SaveChangesAsync(ct);
    }
}

/// <summary>The I-FORM Aluminium Formwork Accessories catalogue extracted from the supplied PDF/Excel.</summary>
public static class AccessoryCatalogue
{
    public static readonly IReadOnlyList<ProductImportRow> All = Build();

    private static List<ProductImportRow> Build() => new()
    {
        // ---------- SNAP TIE ----------
        new("DAAA", "Snap Tie 4'", "Snap Tie", "Wall thickness (mm)", "Steel", "nos", null),
        new("DABA", "2-Hole Reusable Tie", "Snap Tie", "Wall thickness (mm)", "Steel", "nos", null),
        new("DACA", "3-Hole Reusable Tie (W37)", "Snap Tie", "Wall thickness (mm)", "Steel", "nos", null),
        new("DAHA", "3-Hole Reusable Tie (W33)", "Snap Tie", "Wall thickness (mm)", "Steel", "nos", null),

        // ---------- RE-CONE TIE ----------
        new("DTGD", "Re-Cone Tie", "Re-Cone Tie", "[1/2] - Wall thickness (mm)", "Steel + PVC", "nos", null),

        // ---------- T-TIE ----------
        new("DADA", "T-Tie", "T-Tie", "Wall thickness (mm)", "Steel", "nos", null),

        // ---------- DOUBLE POUR TIE ----------
        new("DAFA", "Double Pour Tie", "Double Pour Tie", "Wall th'k - Wall space distance", "Steel", "nos", null),

        // ---------- AL-ROD TIE / TIE ROD ----------
        new("DAGA", "Al-Rod Tie", "Tie Rod", "Wall thickness (mm)", "Steel", "nos", null),
        new("DAGB", "Tie Rod (1/2)", "Tie Rod", "Length", "Steel", "nos", null),
        new("DAGC", "Tie Rod (5/8)", "Tie Rod", "Length", "Steel", "nos", null),

        // ---------- SEPA BOLT ----------
        new("DAIB", "Sepa Bolt (1/2)", "Sepa Bolt", "- Wall thickness (mm)", "Steel", "nos", null),
        new("DAIC", "Sepa Bolt (5/8)", "Sepa Bolt", "- Wall thickness (mm)", "Steel", "nos", null),

        // ---------- PROP / SUPPORT ----------
        new("DRVA0001", "Support (V1)", "Support", "3.1 m extended", "Steel", "nos", null),
        new("DRVA0002", "Support (V2)", "Support", "Extended", "Steel", "nos", null),
        new("DRWA0001", "Support (V3)", "Support", "Extended", "Steel", "nos", null),
        new("DRWA0002", "Support (V4)", "Support", "Extended", "Steel", "nos", null),
        new("DRTA0005", "Pipe Head Adaptor", "Support", "Pipe Dia.", "Steel", "nos", null),

        // ---------- D-CONE ----------
        new("DBAA0000", "D-Cone", "D-Cone", "[1/2] - 40MM / [5/8] - 60MM", "Steel + PVC", "nos", null),

        // ---------- PIN ----------
        new("DCAA0001", "Pin (KK-Type)", "Pin", "KK", "Steel", "nos", null),
        new("DCAA0015", "Pin (ALFA-Type)", "Pin", "ASIA", "Steel", "nos", null),
        new("DCAB0059", "Pin (AO-Type)", "Pin", "A-ONE", "Steel", "nos", null),
        new("DCAC0059", "Pin (ALFU-Type)", "Pin", "USA", "Steel", "nos", null),

        // ---------- LONG PIN ----------
        new("DCBA0064", "Long Pin 64L", "Long Pin", "ALF - Form Clip", "Steel", "nos", null),
        new("DCBB0100", "Long Pin 100L", "Long Pin", "HD - 100L", "Steel", "nos", null),
        new("DCBB0150", "Long Pin 150L", "Long Pin", "SM - 150L", "Steel", "nos", null),
        new("DCBB0152", "Long Pin 152L", "Long Pin", "KK - 152L", "Steel", "nos", null),
        new("DCBC0157", "Long Pin 157L", "Long Pin", "ALF - Pin", "Steel", "nos", null),

        // ---------- WEDGE / WEDGE PIN ----------
        new("DCCA0001", "Wedge (ALFA-Type)", "Wedge", "ASIA", "Steel", "nos", null),
        new("DCCB0001", "Wedge (AO-Type)", "Wedge", "A-ONE", "Steel", "nos", null),
        new("DCCC0001", "Straight Wedge (ALFU-Type)", "Wedge", "USA", "Steel", "nos", null),
        new("DCCD0001", "5 Degree Curved Wedge (ALFU-Type)", "Wedge", "USA", "Steel", "nos", null),
        new("DCCE0001", "Curved Wedge (ALFU-Type)", "Wedge", "USA", "Steel", "nos", null),

        // ---------- WALER BRACKET ----------
        new("DDAA0001", "Adjustable Waler Bracket (ALFA-Type)", "Waler Bracket", "50x50", "Steel", "nos", null),
        new("DDAA0003", "Adjustable Waler Bracket (ALFU-Type)", "Waler Bracket", "2x4", "Steel", "nos", null),
        new("DDBA0001", "Std. Waler (ALFU-Type)", "Waler Bracket", "2x4", "Steel", "nos", null),

        // ---------- WALER BOARD ----------
        new("DRMA", "Waler Board", "Waler Board", "50x50x3.2t - Length (M)", "Steel", "m", null),

        // ---------- KL BRACKET ----------
        new("DDCA0099", "KL Bracket \"U\" Type - 99.2MM", "KL Bracket", "U-99.2MM", "Steel", "nos", null),
        new("DDCB0099", "KL Bracket \"Z\" Type - 99.2MM", "KL Bracket", "Z-99.2MM", "Steel", "nos", null),
        new("DDCE0092", "KL Bracket \"U\" Type - 92.5MM", "KL Bracket", "U-92.5MM", "Steel", "nos", null),
        new("DDCF0092", "KL Bracket \"Z\" Type - 92.5MM", "KL Bracket", "Z-92.5MM", "Steel", "nos", null),

        // ---------- WALL BRACKET ----------
        new("DEAA0600", "Std. Wall Bracket (DYVIDAG-Type)", "Wall Bracket", "1150X1000X600", "Steel", "nos", null),
        new("DEAA0740", "Wall Bracket (TIE-Type)", "Wall Bracket", "1070X950X740", "Steel", "nos", null),
        new("DEBA1000", "Slab Bracket", "Wall Bracket", "1150X1000", "Steel", "nos", null),
        new("DECA0245", "Special Wall Bracket", "Wall Bracket", "1150X1000X245", "Steel", "nos", null),

        // ---------- KICKER ANCHOR ----------
        new("DFAB1600", "Kicker Anchor Nut", "Kicker Anchor", "M16 x 2.0", "Steel", "nos", null),
        new("DFAB1601", "Kicker Anchor Washer", "Kicker Anchor", "M16", "Steel", "nos", null),
        new("DFAB1610", "Anchor Sleeve 100MM", "Kicker Anchor", "100MM", "PVC", "nos", null),
        new("DFAB1675", "Kicker Anchor Bolt", "Kicker Anchor", "M16x75L", "Steel", "nos", null),

        // ---------- HEX BOLT / DYVIDAG BOLT ----------
        new("DFAC1610", "DYVIDAG Kicker Anchor Bolt", "DYVIDAG Bolt", "100mm", "Steel", "nos", null),
        new("DFAC1611", "DYVIDAG Kicker Anchor Al-Nut", "DYVIDAG Bolt", "Al-Nut", "Aluminum", "nos", null),
        new("DFAC1635", "Panel Join Bolt", "DYVIDAG Bolt", "M16x35", "Steel", "nos", null),
        new("DFAC1636", "Panel Join Nut", "DYVIDAG Bolt", "M16", "Steel", "nos", null),
        new("DFAE", "DYVIDAG Bolt", "DYVIDAG Bolt", "17Ø x Length", "Steel", "nos", null),

        // ---------- WALER FIXING BOLT ----------
        new("DFAF0150", "Waler Fixing Bolt (HEX Bolt-Type)", "Waler Fixing Bolt", "M16*35 - Length", "Steel", "nos", null),
        new("DFAG0200", "Waler Fixing Bolt (Pin-Type) - 5/8", "Waler Fixing Bolt", "Length", "Steel", "nos", null),
        new("DFAH2012", "Waler Fixing Bolt (Pin-Type) - 1/2", "Waler Fixing Bolt", "Length", "Steel", "nos", null),

        // ---------- WING NUT ----------
        new("DHAA0001", "Wing Nut 1/2", "Wing Nut", "1/2\"", "Cast-iron", "nos", null),
        new("DHBA0001", "Wing Nut 5/8", "Wing Nut", "5/8\"", "Cast-iron", "nos", null),

        // ---------- BRACKET BOLT ----------
        new("DFAA", "Bracket Bolt", "Bracket Bolt", "17Ø x Length", "Steel", "nos", null),

        // ---------- FORM CLIP ----------
        new("DIAA0001", "Form Clip-LH (ALFA-Type)", "Form Clip", "LH (Asia)", "Steel", "nos", null),
        new("DIAB0001", "Form Clip-RH (ALFA-Type)", "Form Clip", "RH (Asia)", "Steel", "nos", null),
        new("DIBA0001", "Form Clip-LH (ALFU-Type)", "Form Clip", "LH (USA)", "Steel", "nos", null),
        new("DIBB0001", "Form Clip-RH (ALFU-Type)", "Form Clip", "RH (USA)", "Steel", "nos", null),

        // ---------- PIN LOCK ----------
        new("DJAC0001", "Pin Lock PVC Cylinder", "Pin Lock", "PVC Cylinder", "PVC", "nos", null),
        new("DJBA0001", "Pin Lock LH-16.5 (Wall)", "Pin Lock", "LH (Asia)", "Steel + PVC", "nos", null),
        new("DJBB0001", "Pin Lock RH-16.5 (Wall)", "Pin Lock", "RH (Asia)", "Steel + PVC", "nos", null),

        // ---------- PVC TIE SLEEVE / PVC PIPE ----------
        new("DKAA", "PVC Tie Sleeve", "PVC Sleeve", "Wall thickness (mm)", "PVC", "nos", null),
        new("DLAA0000", "PVC Pipe 22Ø, 2M", "PVC Pipe", "22Ø / 2M", "PVC", "m", null),
        new("DLAA0002", "PVC Pipe [1/2, 2M]", "PVC Pipe", "[1/2 - 2M]", "PVC", "m", null),
        new("DLAA0003", "PVC Pipe [5/8, 2M]", "PVC Pipe", "[5/8 - 2M]", "PVC", "m", null),

        // ---------- DOOR BRACE ----------
        new("DQAA04000900", "Door Brace 400~900", "Door Brace", "400~900", "Steel", "nos", null),
        new("DQAA05000700", "Door Brace 500~700", "Door Brace", "600", "Steel", "nos", null),
        new("DQAA06000800", "Door Brace 600~800", "Door Brace", "600~800", "Steel", "nos", null),
        new("DQAA07000900", "Door Brace 700~900", "Door Brace", "700~900", "Steel", "nos", null),
        new("DQAA07001100", "Door Brace 700~1100", "Door Brace", "700~1100", "Steel", "nos", null),
        new("DQAA07500950", "Door Brace 750~950", "Door Brace", "750~950", "Steel", "nos", null),
        new("DQAA09001100", "Door Brace 900~1100", "Door Brace", "900~1100", "Steel", "nos", null),
        new("DQAA09001600", "Door Brace 900~1600", "Door Brace", "900~1600", "Steel", "nos", null),
        new("DQAA09501100", "Door Brace 950~1100", "Door Brace", "950~1100", "Steel", "nos", null),
        new("DQAA10501200", "Door Brace 1050~1200", "Door Brace", "1050~1200", "Steel", "nos", null),
        new("DQAA11001300", "Door Brace 1100~1300", "Door Brace", "1100~1300", "Steel", "nos", null),
        new("DQAA11501300", "Door Brace 1150~1300", "Door Brace", "1150~1300", "Steel", "nos", null),
        new("DQAA12001400", "Door Brace 1200~1400", "Door Brace", "1200~1400", "Steel", "nos", null),
        new("DQAA14001600", "Door Brace 1400~1600", "Door Brace", "1400~1600", "Steel", "nos", null),
        new("DQAA16001800", "Door Brace 1600~1800", "Door Brace", "1600~1800", "Steel", "nos", null),
        new("DQAA18002000", "Door Brace 1800~2000", "Door Brace", "1800~2000", "Steel", "nos", null),

        // ---------- LOW CONTROL BRACE ----------
        new("DEDA0001", "Low Control Brace", "Low Control Brace", "600L", "Steel", "nos", null),

        // ---------- PLUMBING WALL BRACE ----------
        new("DQAE2000", "Plumbing Wall Brace 2000 [2400H]", "Plumbing Wall Brace", "2000 [2400H]", "Steel", "nos", null),
        new("DQAE2200", "Plumbing Wall Brace 2200 [3000H]", "Plumbing Wall Brace", "2200 [3000H]", "Steel", "nos", null),
        new("DQAE2700", "Plumbing Wall Brace 2700 [3500H]", "Plumbing Wall Brace", "2700 [3500H]", "Steel", "nos", null),
        new("DQAE2800", "Plumbing Wall Brace 2800 [3500H]", "Plumbing Wall Brace", "2800 [3500H]", "Steel", "nos", null),
        new("DQAG3000", "Plumbing Wall Brace 3000", "Plumbing Wall Brace", "3000", "Steel", "nos", null),

        // ---------- PUSH-PULL BRACING / CAP BRACES ----------
        new("DZAA", "Push-Pull Bracing Set", "Push-Pull Bracing", "Long 1800L & Short 800L", "Steel", "nos", null),
        new("DQAB0001", "Cap Braces (ALFU-Type)", "Cap Brace", "STD (USA)", "Steel", "nos", null),
        new("DQAB0700", "Cap Braces (Special)", "Cap Brace", "Special (700)", "Steel", "nos", null),
        new("DQAF0600", "Cap Braces (ALFA-Type)", "Cap Brace", "STD (Asia)", "Steel", "nos", null),

        // ---------- TIE KEEPER ----------
        new("DPAA0001", "Tie Keeper (Omniwedge)", "Tie Keeper", "Omniwedge", "Steel", "nos", null),

        // ---------- TOOLS & ETC ----------
        new("DRAA1710", "Bracket Flange Nut", "Tools", "17-100Ø", "Cast-iron", "nos", null),
        new("DRBA0001", "Tie Puller", "Tools", "Standard", "Steel", "nos", null),
        new("DRAA0001", "Pin Lock Stripping Tool", "Tools", "Standard", "Cast-iron", "nos", null),
        new("DRCA0002", "Panel Puller", "Tools", "Y style", "Steel", "nos", null),
        new("DRDA0001", "Hole Aligner", "Tools", "Standard", "Steel", "nos", null),
        new("DRFA0001", "Tie Breaker Bar", "Tools", "Standard", "Steel", "nos", null),
        new("DRGA0001", "Sleeve Eject Bar", "Tools", "Standard", "Steel", "nos", null),
        new("DRNA0002", "Work Bench (1000H)", "Tools", "1200x500x1000 (H)", "Steel", "nos", null),
        new("DRNA0004", "Work Bench (750H)", "Tools", "1200X500X750 (H)", "Steel", "nos", null),
        new("DROB0001", "Wire Turnbuckle", "Tools", "5/8*6M", "Steel", "nos", null),

        // ---------- FITTINGS ----------
        new("DTGA0001", "PVC Cone", "Fittings", "Standard", "PVC", "nos", null),
        new("DUAA0001", "Square Washer", "Fittings", "Standard", "Steel", "nos", null),
        new("DZAA0004", "Double Waler Nut Clamp", "Fittings", "Standard", "Steel", "nos", null),
        new("DZAA0005", "Double Waler Clamp Washer", "Fittings", "130X50", "Steel", "nos", null),
        new("DZAA0006", "Plastic Cap 16Ø", "Fittings", "16Ø", "PVC", "nos", null),
        new("DZAA0008", "Plastic Cap 18Ø", "Fittings", "18Ø", "PVC", "nos", null)
    };
}
