using SiteQueryDefectTracking.Mobile.Models;
using SiteQueryDefectTracking.Mobile.Services;

namespace SiteQueryDefectTracking.Mobile.Pages;

public partial class SearchPage : ContentPage
{
    private readonly QueryService _queries;
    private readonly ApiClient _api;
    private readonly ProjectService _projects;
    private readonly ReferenceService _reference;

    private List<LookupItem> _projectLookup = new();
    private List<LookupItem> _issueTypes = new();

    public SearchPage(QueryService queries, ApiClient api, ProjectService projects, ReferenceService reference)
    {
        InitializeComponent();
        _queries = queries;
        _api = api;
        _projects = projects;
        _reference = reference;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadReferencesAsync();
        await RunSearchAsync();
    }

    private async Task LoadReferencesAsync()
    {
        _projectLookup = await _projects.GetActiveAsync();
        _issueTypes = await _reference.GetIssueTypesAsync();
    }

    private async Task RunSearchAsync()
    {
        try
        {
            var result = await _queries.SearchAsync(new QuerySearchPayload
            {
                Keyword = KeywordSearch.Text?.Trim(),
                PageSize = 50
            });
            QueryListView.ItemsSource = result.Items;
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Search failed", ex.Message, "OK");
        }
    }

    private async void OnSearchClicked(object? sender, EventArgs e)
        => await RunSearchAsync();

    private async void OnRefreshClicked(object? sender, EventArgs e)
        => await RunSearchAsync();

    private async void OnFiltersClicked(object? sender, EventArgs e)
    {
        var status = await AskPickerAsync("Status", new[] { "Any status", "Pending", "In Progress", "Resolved" });
        var issueType = await AskPickerAsync("Issue type",
            _issueTypes.Select(t => t.Name).Prepend("All issue types").ToArray());
        var project = await AskPickerAsync("Project",
            _projectLookup.Select(p => p.Name).Prepend("All projects").ToArray());

        QueryStatus? statusFilter = status switch
        {
            "Pending" => QueryStatus.Pending,
            "In Progress" => QueryStatus.InProgress,
            "Resolved" => QueryStatus.Resolved,
            _ => null
        };

        Guid? projectId = project == "All projects" ? null : _projectLookup.First(p => p.Name == project).Id;
        Guid? issueTypeId = issueType == "All issue types" ? null : _issueTypes.First(t => t.Name == issueType).Id;

        try
        {
            var result = await _queries.SearchAsync(new QuerySearchPayload
            {
                Status = statusFilter,
                ProjectId = projectId,
                IssueTypeId = issueTypeId,
                PageSize = 50
            });
            QueryListView.ItemsSource = result.Items;
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Search failed", ex.Message, "OK");
        }
    }

    private async Task<string> AskPickerAsync(string title, string[] options)
    {
        var result = await DisplayActionSheetAsync(title, "Cancel", null, options);
        return result ?? "Any status";
    }

    private async void OnQuerySelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.Count == 0 || e.CurrentSelection.FirstOrDefault() is not QuerySummaryDto selected)
        {
            return;
        }

        QueryListView.SelectedItem = null;
        await Navigation.PushAsync(new QueryDetailPage(_queries, _api, selected.Id));
    }
}