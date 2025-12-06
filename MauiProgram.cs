using Microsoft.Extensions.Logging;

namespace Zinote;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			})
            .ConfigureMauiHandlers(handlers =>
            {
            });
            
            // Remove borders/underlines on all platforms
            Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("NoBorder", (handler, view) =>
            {
#if ANDROID
                handler.PlatformView.Background = null;
                handler.PlatformView.SetBackgroundColor(Android.Graphics.Color.Transparent);
                handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
#elif IOS || MACCATALYST
                handler.PlatformView.BorderStyle = UIKit.UITextBorderStyle.None;
#elif WINDOWS
                handler.PlatformView.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
                handler.PlatformView.Background = null;
                // These are critical for removing the blue focus rectangle on Windows
                handler.PlatformView.FocusVisualPrimaryThickness = new Microsoft.UI.Xaml.Thickness(0);
                handler.PlatformView.FocusVisualSecondaryThickness = new Microsoft.UI.Xaml.Thickness(0);
                handler.PlatformView.UseSystemFocusVisuals = false; 
                handler.PlatformView.BorderBrush = null; // Sometimes needed
#endif
            });

            Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping("NoBorder", (handler, view) =>
            {
#if ANDROID
                handler.PlatformView.Background = null;
                handler.PlatformView.SetBackgroundColor(Android.Graphics.Color.Transparent);
                handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
#elif IOS || MACCATALYST
                handler.PlatformView.Layer.BorderWidth = 0;
#elif WINDOWS
                handler.PlatformView.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
                handler.PlatformView.Background = null;
                handler.PlatformView.FocusVisualPrimaryThickness = new Microsoft.UI.Xaml.Thickness(0);
                handler.PlatformView.FocusVisualSecondaryThickness = new Microsoft.UI.Xaml.Thickness(0);
                handler.PlatformView.UseSystemFocusVisuals = false;
                handler.PlatformView.BorderBrush = null;
#endif
            });

#if DEBUG
		builder.Logging.AddDebug();
#endif
            builder.Services.AddSingleton<Services.AuthService>();
            builder.Services.AddSingleton<Services.DataService>();
            builder.Services.AddTransient<Services.ExportService>();
            builder.Services.AddTransient<Services.ImportService>();
            builder.Services.AddTransient<Pages.DictionaryListPage>();
            builder.Services.AddTransient<Pages.ItemDetailPage>();
            builder.Services.AddTransient<Pages.HubPage>();
            builder.Services.AddTransient<Pages.LoginPage>();

		return builder.Build();
	}
}
