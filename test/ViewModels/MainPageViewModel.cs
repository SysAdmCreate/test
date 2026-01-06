using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.ApplicationModel;
using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;
using Plugin.BLE.Abstractions;
using test.Services;

namespace test.ViewModels;

public sealed class MainPageViewModel : INotifyPropertyChanged, IDisposable
{
	private readonly IAdapter _adapter;
	private readonly DeviceConnectionService _connectionService;
	private readonly HashSet<Guid> _seenDeviceIds = new();
	private bool _isScanning;
	private string _statusText = "Готово до пошуку.";
	private bool _isConnecting;
	private BleDeviceViewModel? _selectedDevice;

	public event PropertyChangedEventHandler? PropertyChanged;

	public ObservableCollection<BleDeviceViewModel> Devices { get; } = new();

	public Command ScanCommand { get; }
	public Command OpenDeviceDetailCommand { get; }

	public bool IsScanning
	{
		get => _isScanning;
		private set
		{
			if (_isScanning == value)
				return;

			_isScanning = value;
			OnPropertyChanged();
			OnPropertyChanged(nameof(ScanButtonText));
		}
	}

	public string StatusText
	{
		get => _statusText;
		private set
		{
			if (_statusText == value)
				return;

			_statusText = value;
			OnPropertyChanged();
		}
	}

	public string ScanButtonText => IsScanning ? "Зупинити пошук" : "Пошук BLE пристроїв";

	public string FooterText => Devices.Count == 0
		? "Поки що пристроїв немає."
		: $"Знайдено: {Devices.Count}";

	public BleDeviceViewModel? SelectedDevice
	{
		get => _selectedDevice;
		set
		{
			if (_selectedDevice == value)
				return;

			_selectedDevice = value;
			OnPropertyChanged();

			if (value is null)
				return;

			_ = ConnectAndOpenDetailAsync(value);
		}
	}

	public MainPageViewModel(IAdapter adapter, DeviceConnectionService connectionService)
	{
		_adapter = adapter;
		_connectionService = connectionService;
		_adapter.ScanTimeout = 10000;
		_adapter.ScanMode = ScanMode.LowLatency;
		_adapter.DeviceDiscovered += OnDeviceDiscovered;

		ScanCommand = new Command(async () => await OnScanAsync());
		OpenDeviceDetailCommand = new Command(async () => await OpenDeviceDetailAsync());
	}

	public void Dispose()
	{
		_adapter.DeviceDiscovered -= OnDeviceDiscovered;
	}

	private async Task OnScanAsync()
	{
		if (IsScanning)
		{
			await StopScanAsync();
			return;
		}

		await StartScanAsync();
	}

	private async Task StartScanAsync()
	{
		if (!await EnsureBlePermissionsAsync())
		{
			StatusText = "Потрібні дозволи Bluetooth.";
			return;
		}

		var ble = CrossBluetoothLE.Current;
		if (ble.State != BluetoothState.On)
		{
			StatusText = "Bluetooth вимкнено.";
			return;
		}

		Devices.Clear();
		_seenDeviceIds.Clear();
		IsScanning = true;
		UpdateFooter();
		StatusText = "Сканування...";

		try
		{
			await _adapter.StartScanningForDevicesAsync();
			StatusText = "Сканування завершено.";
		}
		catch (Exception ex)
		{
			StatusText = $"Помилка сканування: {ex.Message}";
		}
		finally
		{
			IsScanning = false;
			UpdateFooter();
		}
	}

	private async Task StopScanAsync()
	{
		try
		{
			await _adapter.StopScanningForDevicesAsync();
		}
		catch
		{
			// Ignore stop errors.
		}
		finally
		{
			IsScanning = false;
			StatusText = "Сканування зупинено.";
		}
	}

	private void OnDeviceDiscovered(object? sender, DeviceEventArgs e)
	{
		if (!_seenDeviceIds.Add(e.Device.Id))
			return;

		MainThread.BeginInvokeOnMainThread(() =>
		{
			Devices.Add(new BleDeviceViewModel(e.Device));
			UpdateFooter();
		});
	}

	private void UpdateFooter()
	{
		OnPropertyChanged(nameof(FooterText));
	}

	private async Task OpenDeviceDetailAsync()
	{
		await MainThread.InvokeOnMainThreadAsync(() => Shell.Current.GoToAsync("deviceDetail"));
	}

	private async Task ConnectAndOpenDetailAsync(BleDeviceViewModel deviceViewModel)
	{
		if (_isConnecting)
			return;

		_isConnecting = true;
		StatusText = "Підключення...";

		try
		{
			await StopScanAsync();

			var connected = await _connectionService.ConnectAndMaintainAsync(_adapter, deviceViewModel.Device);
			if (!connected)
			{
				StatusText = $"Не вдалося під'єднатись: {_connectionService.LastError}";
				return;
			}

			StatusText = $"Під'єднано: {deviceViewModel.Name}";
			await MainThread.InvokeOnMainThreadAsync(() => Shell.Current.GoToAsync("deviceDetail"));
		}
		finally
		{
			_isConnecting = false;
			ClearSelection();
		}
	}

	private void ClearSelection()
	{
		if (_selectedDevice is null)
			return;

		_selectedDevice = null;
		OnPropertyChanged(nameof(SelectedDevice));
	}

	private async Task<bool> EnsureBlePermissionsAsync()
	{
#if ANDROID
		if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.S)
		{
			var scanStatus = await Permissions.CheckStatusAsync<BluetoothScanPermission>();
			if (scanStatus != PermissionStatus.Granted)
				scanStatus = await Permissions.RequestAsync<BluetoothScanPermission>();

			var connectStatus = await Permissions.CheckStatusAsync<BluetoothConnectPermission>();
			if (connectStatus != PermissionStatus.Granted)
				connectStatus = await Permissions.RequestAsync<BluetoothConnectPermission>();

			return scanStatus == PermissionStatus.Granted && connectStatus == PermissionStatus.Granted;
		}

		var locationStatus = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
		if (locationStatus != PermissionStatus.Granted)
			locationStatus = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

		return locationStatus == PermissionStatus.Granted;
#else
		return true;
#endif
	}

	private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

#if ANDROID
	private sealed class BluetoothScanPermission : Permissions.BasePlatformPermission
	{
		public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
			new[] { (Android.Manifest.Permission.BluetoothScan, true) };
	}

	private sealed class BluetoothConnectPermission : Permissions.BasePlatformPermission
	{
		public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
			new[] { (Android.Manifest.Permission.BluetoothConnect, true) };
	}
#endif
}

public sealed class BleDeviceViewModel
{
	public BleDeviceViewModel(IDevice device)
	{
		Device = device;
		Name = string.IsNullOrWhiteSpace(device.Name) ? "Невідомий пристрій" : device.Name;
		Id = device.Id.ToString();
		RssiText = device.Rssi == 0 ? "N/A" : $"{device.Rssi} dBm";
	}

	public IDevice Device { get; }
	public string Name { get; }
	public string Id { get; }
	public string RssiText { get; }
}
