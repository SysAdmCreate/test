namespace test;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		Routing.RegisterRoute("deviceDetail", typeof(DeviceDetailPage));
	}
}
