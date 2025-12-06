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
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif
            builder.Services.AddSingleton<Services.DataService>();
            builder.Services.AddTransient<Services.ExportService>();
            builder.Services.AddTransient<Pages.DictionaryListPage>();
            builder.Services.AddTransient<Pages.ItemDetailPage>();
            builder.Services.AddTransient<Pages.HubPage>();

		return builder.Build();
	}
}
