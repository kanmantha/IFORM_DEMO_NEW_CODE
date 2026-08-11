using SiteQueryDefectTracking.Mobile.Models;
using SiteQueryDefectTracking.Mobile.Services;

namespace SiteQueryDefectTracking.Mobile.Pages;

public partial class QueryDetailPage : ContentPage
{
    private readonly QueryService _queries;
    private readonly ApiClient _api;
    private readonly Guid _queryId;

    private QueryDetailDto? _detail;

    public QueryDetailPage(QueryService queries, ApiClient api, Guid queryId)
    {
        InitializeComponent();
        _queries = queries;
        _api = api;
        _queryId = queryId;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            _detail = await _queries.GetAsync(_queryId);
            if (_detail is null)
            {
                await DisplayAlertAsync("Error", "Query not found.", "OK");
                return;
            }

            Populate(_detail);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    private async void Populate(QueryDetailDto d)
    {
        IpoLabel.Text = $"IPO {d.IPO}";
        StatusLabel.Text = d.Status switch
        {
            QueryStatus.Pending => "● Pending",
            QueryStatus.InProgress => "● In Progress",
            _ => "● Resolved"
        };
        StatusLabel.TextColor = d.Status == QueryStatus.Pending ? Color.FromArgb("#E67E22")
            : d.Status == QueryStatus.InProgress ? Color.FromArgb("#2980B9")
            : Color.FromArgb("#27AE60");

        ProjectLabel.Text = $"Project: {d.ProjectName}";
        IssueTypeLabel.Text = $"Issue: {d.IssueTypeName}";
        QuantityLabel.Text = $"Quantity: {d.QuantityNos} nos{(d.QuantitySqm.HasValue ? $" / {d.QuantitySqm} sqm" : string.Empty)}";

        if (!string.IsNullOrWhiteSpace(d.ProductCode))
        {
            ProductLabel.Text = $"Product: {d.ProductCode}";
        }
        if (!string.IsNullOrWhiteSpace(d.DispatchStatus))
        {
            DispatchLabel.Text = $"Dispatch: {d.DispatchStatus}";
        }

        RaisedLabel.Text = $"Raised by {d.RaisedByName} on {d.RaiseDate:dd MMM yyyy}";
        DelayLabel.Text = d.IsSlaBreached
            ? $"SLA BREACHED — Delay {d.DelayDays} days"
            : $"Delay: {d.DelayDays} days";
        DelayLabel.TextColor = d.IsSlaBreached ? Colors.Red : Colors.Gray;

        if (!string.IsNullOrWhiteSpace(d.Description))
        {
            DescriptionLabel.Text = d.Description;
            DescriptionLabel.IsVisible = true;
        }

        CommentsList.ItemsSource = d.Comments;

        var photoPaths = new List<string>();
        foreach (var att in d.Attachments.Where(a => a.Type == "Photo"))
        {
            var path = await _queries.GetPhotoUrlAsync(d.Id, att.Id);
            if (path is not null)
            {
                photoPaths.Add(path);
            }
        }
        if (photoPaths.Count > 0)
        {
            PhotoList.ItemsSource = photoPaths;
            PhotoList.IsVisible = true;
        }

        ResolveButton.IsVisible = d.Status != QueryStatus.Resolved;
    }

    private async void OnAddCommentClicked(object? sender, EventArgs e)
    {
        var text = CommentEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;

        try
        {
            await _queries.AddCommentAsync(_queryId, text);
            CommentEntry.Text = null;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    private async void OnResolveClicked(object? sender, EventArgs e)
    {
        var note = await DisplayPromptAsync("Resolve", "Resolution note (optional)", "Resolve", "Cancel");
        if (note is null) return;

        try
        {
            await _queries.ResolveAsync(_queryId, note);
            await DisplayAlertAsync("Done", "Query marked as resolved.", "OK");
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    private async void OnEmailClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new EmailPage(_api, _queryId));
    }
}