using SiteQueryDefectTracking.Mobile.Services;

namespace SiteQueryDefectTracking.Mobile.Pages;

public partial class EmailPage : ContentPage
{
    private readonly ApiClient _api;
    private readonly Guid _queryId;
    private readonly EmailService _email;

    private List<Models.EmailTemplateDto> _templates = new();
    private Guid? _generatedTemplateId;

    public EmailPage(ApiClient api, Guid queryId)
    {
        InitializeComponent();
        _api = api;
        _queryId = queryId;
        _email = new EmailService(api);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        QueryLabel.Text = $"Query {_queryId.ToString("N")[..8].ToUpperInvariant()}";

        if (!_api.IsAuthenticated)
        {
            return;
        }

        if (!_api.HasRole("Manager"))
        {
            StatusLabel.Text = "Email center is available to Managers only. Request a manager to send the email.";
            StatusLabel.TextColor = Colors.Orange;
            StatusLabel.IsVisible = true;
            TemplatePicker.IsVisible = false;
            SubjectEntry.IsVisible = false;
            BodyEditor.IsVisible = false;
            RecipientEntry.IsVisible = false;
            return;
        }

        await LoadTemplatesAsync();
    }

    private async Task LoadTemplatesAsync()
    {
        try
        {
            var templates = await _email.GetTemplatesAsync();
            if (templates is null || templates.Count == 0)
            {
                StatusLabel.Text = "No email templates configured.";
                StatusLabel.IsVisible = true;
                return;
            }

            _templates = templates;
            TemplatePicker.ItemsSource = templates.Select(t => t.Name).ToList();
            TemplatePicker.SelectedIndex = Math.Max(0, templates.FindIndex(t => t.IsDefault));
        }
        catch (Exception ex)
        {
            StatusLabel.Text = ex.Message;
            StatusLabel.TextColor = Colors.Red;
            StatusLabel.IsVisible = true;
        }
    }

    private async void OnGenerateClicked(object? sender, EventArgs e)
        => await GenerateDraftAsync();

    private async Task GenerateDraftAsync()
    {
        if (TemplatePicker.SelectedIndex < 0)
        {
            await DisplayAlertAsync("Template required", "Select a template first.", "OK");
            return;
        }

        var template = _templates[TemplatePicker.SelectedIndex];
        try
        {
            var draft = await _email.GenerateAsync(_queryId, template.Id, RecipientEntry.Text?.Trim());
            if (draft is null) return;

            _generatedTemplateId = draft.TemplateId;
            SubjectEntry.Text = draft.Subject;
            BodyEditor.Text = draft.Body;
            if (string.IsNullOrWhiteSpace(RecipientEntry.Text))
            {
                RecipientEntry.Text = draft.To;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Generate failed", ex.Message, "OK");
        }
    }

    private async void OnSendClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(RecipientEntry.Text) || string.IsNullOrWhiteSpace(SubjectEntry.Text))
        {
            await DisplayAlertAsync("Missing fields", "Recipient and subject are required.", "OK");
            return;
        }

        try
        {
            await _email.SendAsync(_queryId, RecipientEntry.Text.Trim(), SubjectEntry.Text, BodyEditor.Text ?? string.Empty, _generatedTemplateId);
            StatusLabel.Text = "Email sent.";
            StatusLabel.TextColor = Colors.Green;
            StatusLabel.IsVisible = true;
        }
        catch (Exception ex)
        {
            StatusLabel.Text = ex.Message;
            StatusLabel.TextColor = Colors.Red;
            StatusLabel.IsVisible = true;
        }
    }

    private async void OnDraftClicked(object? sender, EventArgs e)
    {
        await GenerateDraftAsync();
        StatusLabel.Text = "Draft prepared — you can copy the subject and body above.";
        StatusLabel.TextColor = Colors.Green;
        StatusLabel.IsVisible = true;
    }
}