using SiteQueryDefectTracking.Mobile.Models;
using SiteQueryDefectTracking.Mobile.Services;

namespace SiteQueryDefectTracking.Mobile.Pages;

public partial class DashboardPage : ContentPage
{
    private readonly DashboardService _dashboard;
    private readonly ApiClient _api;

    public DashboardPage(DashboardService dashboard, ApiClient api)
    {
        InitializeComponent();
        _dashboard = dashboard;
        _api = api;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async void OnRefreshClicked(object? sender, EventArgs e)
        => await LoadAsync();

    private async Task LoadAsync()
    {
        if (!_api.HasRole("Manager"))
        {
            HeaderLabel.Text = "Dashboard (Manager access required)";
            OpenLabel.Text = "—";
            CriticalLabel.Text = "—";
            ResolvedLabel.Text = "—";
            AvgLabel.Text = "—";
            MaxLabel.Text = "—";
            TodayLabel.Text = "—";
            var denied = new Label
            {
                Text = "The dashboard is available to Managers only.",
                FontSize = 14,
                TextColor = Colors.Orange,
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 20, 0, 0)
            };
            OpenQueriesPanel.Children.Clear();
            OpenQueriesPanel.Children.Add(denied);
            return;
        }

        try
        {
            var snapshot = await _dashboard.GetSnapshotAsync();
            if (snapshot?.Summary is null)
            {
                return;
            }

            var s = snapshot.Summary;
            HeaderLabel.Text = "Dashboard";
            OpenLabel.Text = s.TotalOpenQueries.ToString();
            CriticalLabel.Text = s.CriticalDelays.ToString();
            ResolvedLabel.Text = s.ResolvedTotal.ToString();
            AvgLabel.Text = $"{s.AverageDelay:0.#} d";
            MaxLabel.Text = $"{s.MaxDelay} d";
            TodayLabel.Text = s.ResolvedToday.ToString();

            OpenQueriesPanel.Children.Clear();
            if (snapshot.OpenQueries.Count == 0)
            {
                OpenQueriesPanel.Children.Add(new Label
                {
                    Text = "No open queries.",
                    FontSize = 13,
                    TextColor = Colors.Gray,
                    HorizontalOptions = LayoutOptions.Center,
                    Margin = new Thickness(0, 12, 0, 0)
                });
            }

            foreach (var q in snapshot.OpenQueries.Take(8))
            {
                OpenQueriesPanel.Children.Add(CreateQueryCard(q));
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Dashboard", ex.Message, "OK");
        }
    }

    private static Border CreateQueryCard(QuerySummaryDto q)
    {
        var inner = new VerticalStackLayout { Spacing = 3 };
        inner.Children.Add(new Label
        {
            Text = $"{q.IPO}  —  {q.StatusLabel}",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = q.IsSlaBreached ? Colors.Red : Colors.Black
        });
        inner.Children.Add(new Label
        {
            Text = $"{q.ProjectName} • {q.IssueTypeName}",
            FontSize = 11,
            TextColor = Colors.Gray
        });

        return new Border
        {
            Margin = new Thickness(0, 0, 0, 6),
            Padding = new Thickness(12),
            StrokeThickness = 1,
            Stroke = Brush.LightGray,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
            BackgroundColor = Application.Current!.UserAppTheme == AppTheme.Dark
                ? Color.FromArgb("#1E1E1E")
                : Colors.White,
            Content = inner
        };
    }
}