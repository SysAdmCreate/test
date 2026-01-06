using Microsoft.Extensions.DependencyInjection;
using test.Services;
using test.ViewModels;

namespace test;

public partial class DeviceDetailPage : ContentPage
{
	private readonly DeviceDetailViewModel _viewModel;

	public DeviceDetailPage()
	{
		InitializeComponent();

		var services = App.Services;
		var connectionService = services?.GetService<DeviceConnectionService>() ?? new DeviceConnectionService();
		_viewModel = new DeviceDetailViewModel(connectionService);
		BindingContext = _viewModel;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.Refresh();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_viewModel.Dispose();
	}
}
