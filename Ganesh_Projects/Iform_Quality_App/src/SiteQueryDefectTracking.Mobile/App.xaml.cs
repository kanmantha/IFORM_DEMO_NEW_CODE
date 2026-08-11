using SiteQueryDefectTracking.Mobile.Pages;
using SiteQueryDefectTracking.Mobile.Services;

namespace SiteQueryDefectTracking.Mobile;

public partial class App : Application
{
	private readonly ApiClient _api;

	public App(ApiClient api)
	{
		InitializeComponent();
		_api = api;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new LoginPage(_api));
	}
}