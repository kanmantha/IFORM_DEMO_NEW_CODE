using SiteQueryDefectTracking.Mobile.Services;

namespace SiteQueryDefectTracking.Mobile.Pages;

public partial class LoginPage : ContentPage
{
    private readonly ApiClient _api;

    public LoginPage(ApiClient api)
    {
        InitializeComponent();
        _api = api;
    }

    private async void OnLoginClicked(object? sender, EventArgs e)
    {
        var user = UserNameEntry.Text?.Trim();
        var password = PasswordEntry.Text;

        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password))
        {
            await ShowErrorAsync("Enter username and password.");
            return;
        }

        LoginButton.IsEnabled = false;
        StatusLabel.IsVisible = false;
        try
        {
            await _api.LoginAsync(user, password);
            Application.Current!.Windows[0].Page = new AppShell();
        }
        catch (ApiException ex)
        {
            await ShowErrorAsync(ex.Message);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync($"Unable to reach server: {ex.Message}");
        }
        finally
        {
            LoginButton.IsEnabled = true;
        }
    }

    private async Task ShowErrorAsync(string message)
    {
        StatusLabel.Text = message;
        StatusLabel.IsVisible = true;
        await Task.CompletedTask;
    }
}