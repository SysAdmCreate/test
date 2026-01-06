using Microsoft.Extensions.DependencyInjection;
using Plugin.BLE;
using test.Services;
using test.ViewModels;

namespace test;

public partial class MainPage : ContentPage
{
	private readonly MainPageViewModel _viewModel;

	public MainPage()
	{
		InitializeComponent();

		var services = App.Services;
		var connectionService = services?.GetService<DeviceConnectionService>() ?? new DeviceConnectionService();
		_viewModel = new MainPageViewModel(CrossBluetoothLE.Current.Adapter, connectionService);
		BindingContext = _viewModel;
	}

}
