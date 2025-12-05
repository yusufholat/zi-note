namespace Zinote;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();

        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("Borderless", (handler, view) =>
        {
#if WINDOWS
            handler.PlatformView.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
#endif
        });

        Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping("Borderless", (handler, view) =>
        {
#if WINDOWS
            handler.PlatformView.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
#endif
        });

		MainPage = new AppShell();
	}

    protected override Window CreateWindow(IActivationState activationState)
    {
        var window = base.CreateWindow(activationState);

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
