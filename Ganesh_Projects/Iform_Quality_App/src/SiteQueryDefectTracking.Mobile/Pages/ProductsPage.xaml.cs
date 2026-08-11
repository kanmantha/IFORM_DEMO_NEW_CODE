using SiteQueryDefectTracking.Mobile.Models;
using SiteQueryDefectTracking.Mobile.Services;

namespace SiteQueryDefectTracking.Mobile.Pages;

public partial class ProductsPage : ContentPage
{
    private readonly ProductService _products;
    private readonly ApiClient _api;

    public ProductsPage(ProductService products, ApiClient api)
    {
        InitializeComponent();
        _products = products;
        _api = api;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RunSearchAsync(null);
    }

    private async void OnSearchClicked(object? sender, EventArgs e)
        => await RunSearchAsync(ProductSearch.Text?.Trim());

    private async Task RunSearchAsync(string? query)
    {
        try
        {
            var result = await _products.SearchAsync(query, pageSize: 50);
            ProductList.ItemsSource = result?.Items ?? new List<ProductSummaryDto>();
            CountLabel.Text = result is null ? string.Empty
                : $"{result.TotalCount} products";
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Search failed", ex.Message, "OK");
        }
    }

    private async void OnScanClicked(object? sender, EventArgs e)
    {
        await DisplayAlertAsync("Barcode scan", "Attach a scanner or type the code in the search box.", "OK");
    }

    private async void OnProductSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.Count == 0 || e.CurrentSelection.FirstOrDefault() is not ProductSummaryDto selected)
        {
            return;
        }

        ProductList.SelectedItem = null;
        try
        {
            var detail = await _products.GetAsync(selected.Id);
            if (detail is null) return;
            await Navigation.PushAsync(new ProductDetailPage(detail));
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }
}