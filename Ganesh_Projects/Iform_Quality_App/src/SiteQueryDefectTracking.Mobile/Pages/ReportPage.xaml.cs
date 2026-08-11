using SiteQueryDefectTracking.Mobile.Models;
using SiteQueryDefectTracking.Mobile.Services;

namespace SiteQueryDefectTracking.Mobile.Pages;

public partial class ReportPage : ContentPage
{
    private readonly ProjectService _projects;
    private readonly ReferenceService _reference;
    private readonly QueryService _queries;
    private readonly ProductService _products;

    private List<LookupItem> _projectList = new();
    private List<LookupItem> _issueTypeList = new();
    private List<EnumOption> _dispatchStatusList = new();
    private List<ProductSummaryDto> _productList = new();

    private string? _photoPath;
    private string? _photoName;
    private Guid? _selectedProductId;

    public ReportPage(ProjectService projects, ReferenceService reference, QueryService queries, ProductService products)
    {
        InitializeComponent();
        _projects = projects;
        _reference = reference;
        _queries = queries;
        _products = products;

        ProductCodeEntry.TextChanged += OnProductTextChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadLookupsAsync();
    }

    private async Task LoadLookupsAsync()
    {
        try
        {
            _projectList = await _projects.GetActiveAsync();
            _issueTypeList = await _reference.GetIssueTypesAsync();
            _dispatchStatusList = await _reference.GetDispatchStatusesAsync();

            ProjectPicker.ItemsSource = _projectList.Select(p => p.Name).ToList();
            IssueTypePicker.ItemsSource = _issueTypeList.Select(t => t.Name).ToList();
            DispatchStatusPicker.ItemsSource = _dispatchStatusList.Select(d => d.Name).ToList();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Load failed", ex.Message, "OK");
        }
    }

    private async void OnProductTextChanged(object? sender, TextChangedEventArgs e)
    {
        var term = e.NewTextValue?.Trim();
        if (string.IsNullOrWhiteSpace(term) || term.Length < 2)
        {
            ProductResults.IsVisible = false;
            return;
        }

        try
        {
            var result = await _products.SearchAsync(term, pageSize: 15);
            var items = result?.Items;
            if (items is null || items.Count == 0)
            {
                ProductResults.IsVisible = false;
                return;
            }

            _productList = items;
            ProductResults.ItemsSource = items
                .Select(p => $"{p.Code} — {p.Description}")
                .ToList();
            ProductResults.IsVisible = true;
        }
        catch (Exception)
        {
            // Ignore transient search errors while typing.
        }
    }

    private void OnProductSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.Count == 0 || e.CurrentSelection.FirstOrDefault() is not string label)
        {
            return;
        }

        _selectedProductId = _productList.FirstOrDefault(p => $"{p.Code} — {p.Description}" == label)?.Id;
        ProductResults.IsVisible = false;
        ProductCodeEntry.Text = label.Split("—")[0].Trim();
    }

    private async void OnTakePhotoClicked(object? sender, EventArgs e)
    {
        try
        {
            var photo = await MediaPicker.Default.CapturePhotoAsync();
            if (photo is null) return;
            await AttachPhotoAsync(photo);
        }
        catch (PermissionException)
        {
            await DisplayAlertAsync("Permission", "Camera permission denied.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Camera", ex.Message, "OK");
        }
    }

    private async void OnPickPhotoClicked(object? sender, EventArgs e)
    {
        try
        {
            var photos = await MediaPicker.Default.PickPhotosAsync();
            var photo = photos?.FirstOrDefault();
            if (photo is null) return;
            await AttachPhotoAsync(photo);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Gallery", ex.Message, "OK");
        }
    }

    private async Task AttachPhotoAsync(FileResult photo)
    {
        var temp = Path.Combine(Path.GetTempPath(), $"sqd_{Guid.NewGuid():N}{Path.GetExtension(photo.FileName)}");
        await using (var stream = await photo.OpenReadAsync())
        await using (var fs = File.Create(temp))
        {
            await stream.CopyToAsync(fs);
        }

        _photoPath = temp;
        _photoName = photo.FileName;
        PhotoPreview.Source = ImageSource.FromFile(temp);
        PhotoPreview.IsVisible = true;
        PhotoStatus.Text = photo.FileName;
    }

    private async void OnSubmitClicked(object? sender, EventArgs e)
    {
        var projectIndex = ProjectPicker.SelectedIndex;
        var issueIndex = IssueTypePicker.SelectedIndex;

        if (projectIndex < 0 || issueIndex < 0)
        {
            await DisplayAlertAsync("Missing fields", "Select a project and an issue type.", "OK");
            return;
        }

        var ipo = IPOEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(ipo))
        {
            await DisplayAlertAsync("Missing fields", "IPO is required.", "OK");
            return;
        }

        if (!int.TryParse(QuantityNosEntry.Text, out var qtyNos) || qtyNos <= 0)
        {
            await DisplayAlertAsync("Missing fields", "Quantity (Nos) is required.", "OK");
            return;
        }

        decimal? qtySqm = null;
        if (decimal.TryParse(QuantitySqmEntry.Text, out var parsedSqm) && parsedSqm > 0)
        {
            qtySqm = parsedSqm;
        }

        if (_photoPath is null)
        {
            await DisplayAlertAsync("Missing photo", "A photo of the issue is required.", "OK");
            return;
        }

        SubmitButton.IsEnabled = false;
        StatusLabel.IsVisible = false;
        try
        {
            var payload = new CreateQueryPayload
            {
                ProjectId = _projectList[projectIndex].Id,
                IssueTypeId = _issueTypeList[issueIndex].Id,
                IPO = ipo,
                QuantityNos = qtyNos,
                QuantitySqm = qtySqm,
                VerifiedProductCodeId = _selectedProductId,
                DispatchStatus = DispatchStatusPicker.SelectedIndex >= 0
                    ? _dispatchStatusList[DispatchStatusPicker.SelectedIndex].Name
                    : null,
                SlabTarget = string.IsNullOrWhiteSpace(SlabTargetEntry.Text) ? null : SlabTargetEntry.Text.Trim(),
                SlabCompleted = string.IsNullOrWhiteSpace(SlabCompletedEntry.Text) ? null : SlabCompletedEntry.Text.Trim(),
                SlabDelayDays = int.TryParse(SlabDelayEntry.Text, out var slabDelay) ? slabDelay : null,
                Description = DescriptionEditor.Text?.Trim()
            };

            var queryId = await _queries.CreateAsync(payload);

            await _queries.UploadPhotoAsync(queryId, _photoPath, _photoName ?? $"photo{Path.GetExtension(_photoPath)}", "image/jpeg");

            StatusLabel.Text = $"Report {ipo} submitted with photo.";
            StatusLabel.IsVisible = true;
            ClearForm();
        }
        catch (ApiException ex)
        {
            await DisplayAlertAsync("Submit failed", ex.Message, "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Submit failed", ex.Message, "OK");
        }
        finally
        {
            SubmitButton.IsEnabled = true;
        }
    }

    private void ClearForm()
    {
        IPOEntry.Text = null;
        QuantityNosEntry.Text = null;
        QuantitySqmEntry.Text = null;
        ProductCodeEntry.Text = null;
        SlabTargetEntry.Text = null;
        SlabCompletedEntry.Text = null;
        SlabDelayEntry.Text = null;
        DescriptionEditor.Text = null;
        ProjectPicker.SelectedIndex = -1;
        IssueTypePicker.SelectedIndex = -1;
        DispatchStatusPicker.SelectedIndex = -1;
        _selectedProductId = null;
        _photoPath = null;
        _photoName = null;
        PhotoPreview.IsVisible = false;
        PhotoStatus.Text = string.Empty;
    }
}