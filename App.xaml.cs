namespace Zinote;

public partial class App : Application
{
    private readonly Services.AuthService _authService;

    public App(Services.AuthService authService)
    {
        InitializeComponent();
        _authService = authService;
        

    }

    protected override Window CreateWindow(IActivationState activationState)
    {
        // Initial Loading State
        var window = new Window(new ContentPage 
        { 
            Content = new ActivityIndicator 
            { 
                IsRunning = true, 
                VerticalOptions = LayoutOptions.Center, 
                HorizontalOptions = LayoutOptions.Center,
                Color = Colors.Blue
            } 
        });

        // Fire and forget, but safely update UI on MainThread
        Task.Run(async () => 
        {
            // Add slight delay to render loading spinner if needed, or check immediately
            var isAuthenticated = await _authService.CheckLoginStatusAsync();
            
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                window.Page = new AppShell();
                if (isAuthenticated)
                {
                    await Shell.Current.GoToAsync("//HubPage");
                }
                // Implicitly stays on LoginPage (first item) if not authenticated
            });
        });

        const int newWidth = 500;
        const int newHeight = 700;

        window.Width = newWidth;
        window.Height = newHeight;
        window.MinimumWidth = newWidth;
        window.MinimumHeight = newHeight;
        window.MaximumWidth = newWidth;
        window.MaximumHeight = newHeight;

#if WINDOWS
        window.Created += (s, e) =>
        {
            var win = s as Window;
            if (win == null) return;
            
            // Wait for the handler to be created
            win.HandlerChanged += (sender, args) =>
            {
                if (win.Handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindow)
                {
                    var handle = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
                    var id = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(handle);
                    var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId: id);
                    
                    if (appWindow != null)
                    {
                        var presenter = appWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
                        if (presenter != null)
                        {
                            presenter.IsResizable = false;
                            presenter.IsMaximizable = false;
                        }
                    }
                }
            };
        };
#endif

        return window;
    }
}
