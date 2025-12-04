namespace Zinote;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
        Routing.RegisterRoute(nameof(Pages.ItemDetailPage), typeof(Pages.ItemDetailPage));
        Routing.RegisterRoute(nameof(MainPage), typeof(MainPage));
	}
}
