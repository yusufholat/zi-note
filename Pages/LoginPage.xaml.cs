using Zinote.Services;

namespace Zinote.Pages;

public partial class LoginPage : ContentPage
{
	private readonly AuthService _authService;

	public LoginPage(AuthService authService)
	{
		InitializeComponent();
		_authService = authService;
	}

	private async void OnLoginClicked(object sender, EventArgs e)
	{
		if (LoadingOverlay.IsVisible) return;

        string email = EmailEntry.Text;
        string password = PasswordEntry.Text;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            await DisplayAlert("Error", "Please enter email and password.", "OK");
            return;
        }

		try
		{
            SetLoading(true);

			var error = await _authService.LoginAsync(email, password);

			if (string.IsNullOrEmpty(error))
			{
				Application.Current.MainPage = new AppShell();
			}
			else
			{
				await DisplayAlert("Login Failed", error, "OK");
			}
		}
		finally
		{
            SetLoading(false);
		}
	}

    private async void OnRegisterClicked(object sender, EventArgs e)
    {
        if (LoadingOverlay.IsVisible) return;

        string email = EmailEntry.Text;
        string password = PasswordEntry.Text;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            await DisplayAlert("Error", "Please enter email and password to register.", "OK");
            return;
        }

        try
        {
            SetLoading(true);

            var error = await _authService.CreateUserAsync(email, password);

            if (string.IsNullOrEmpty(error))
            {
                await DisplayAlert("Success", "Account created! You are now logged in.", "OK");
                Application.Current.MainPage = new AppShell();
            }
            else
            {
                await DisplayAlert("Registration Failed", error, "OK");
            }
        }
        finally
        {
            SetLoading(false);
        }
    }

    private void OnGuestClicked(object sender, EventArgs e)
    {
        _authService.LoginAsGuest();
        Application.Current.MainPage = new AppShell();
    }

    private void SetLoading(bool isLoading)
    {
        LoadingOverlay.IsVisible = isLoading;
        LoginButton.IsEnabled = !isLoading;
        RegisterButton.IsEnabled = !isLoading;
        GuestButton.IsEnabled = !isLoading;
        EmailEntry.IsEnabled = !isLoading;
        PasswordEntry.IsEnabled = !isLoading;
    }
}
