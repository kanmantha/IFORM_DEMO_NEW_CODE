using IForm.Application.Services;
using Shouldly;

namespace IForm.UnitTests;

public class AccessoryCatalogueTests
{
    [Fact]
    public void Catalogue_is_non_empty()
    {
        AccessoryCatalogue.All.ShouldNotBeEmpty();
    }

    [Fact]
    public void Catalogue_product_codes_are_unique()
    {
        var codes = AccessoryCatalogue.All.Select(x => x.Code).ToList();
        codes.ShouldBeUnique("duplicate product codes break the unique IX_Products_TenantId_ProductCode index");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public void Catalogue_entries_have_required_fields(int index)
    {
        var item = AccessoryCatalogue.All[index - 1];
        item.Code.ShouldNotBeNullOrWhiteSpace();
        item.Name.ShouldNotBeNullOrWhiteSpace();
        item.Unit.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Catalogue_is_importable_to_a_clean_tenant()
    {
        var duplicates = AccessoryCatalogue.All
            .GroupBy(x => x.Code)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        duplicates.ShouldBeEmpty();
    }
}
