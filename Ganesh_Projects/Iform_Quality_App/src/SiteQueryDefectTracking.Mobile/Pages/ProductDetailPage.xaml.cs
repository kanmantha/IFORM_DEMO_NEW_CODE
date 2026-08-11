using SiteQueryDefectTracking.Mobile.Models;

namespace SiteQueryDefectTracking.Mobile.Pages;

public partial class ProductDetailPage : ContentPage
{
    public ProductDetailPage(ProductDetailDto product)
    {
        InitializeComponent();
        CodeLabel.Text = product.Code;
        DescriptionLabel.Text = product.Description;
        CategoryLabel.Text = product.Category ?? "—";
        UnitLabel.Text = product.Unit ?? "—";
        BarcodeLabel.Text = string.IsNullOrWhiteSpace(product.Barcode) ? string.Empty : $"Barcode: {product.Barcode}";
        SpecList.ItemsSource = product.Specifications;
        ProjectList.ItemsSource = product.ProjectMappings;
    }
}