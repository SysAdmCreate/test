using System.ComponentModel;
using System.Runtime.CompilerServices;
using Plugin.BLE.Abstractions.Contracts;
using test.Services;

namespace test.ViewModels;

public sealed class DeviceDetailViewModel : INotifyPropertyChanged, IDisposable
{
	private readonly DeviceConnectionService _connectionService;
	private string _name = "Немає підключення";
	private string _id = string.Empty;
	private string _state = "Невідомо";
	private string _connectedAt = string.Empty;
	private string _errorText = string.Empty;
	private bool _hasError;
	private bool _isConnected;
	private bool _isConnecting;

	public event PropertyChangedEventHandler? PropertyChanged;

	public Command DisconnectCommand { get; }

	public string Name
	{
		get => _name;
		private set => SetField(ref _name, value);
	}

	public string Id
	{
		get => _id;
		private set => SetField(ref _id, value);
	}

	public string ConnectionState
	{
		get => _state;
		private set => SetField(ref _state, value);
	}

	public string ConnectedAtText
	{
		get => _connectedAt;
		private set => SetField(ref _connectedAt, value);
	}

	public string ErrorText
	{
		get => _errorText;
		private set => SetField(ref _errorText, value);
	}

	public bool HasError
	{
		get => _hasError;
		private set => SetField(ref _hasError, value);
	}

	public bool IsConnected
	{
		get => _isConnected;
		private set => SetField(ref _isConnected, value);
	}

	public bool IsConnecting
	{
		get => _isConnecting;
		private set => SetField(ref _isConnecting, value);
	}

	public DeviceDetailViewModel(DeviceConnectionService connectionService)
	{
		_connectionService = connectionService;
		_connectionService.ConnectionStateChanged += OnConnectionStateChanged;
		DisconnectCommand = new Command(async () => await _connectionService.DisconnectAsync());
		Refresh();
	}

	public void Dispose()
	{
		_connectionService.ConnectionStateChanged -= OnConnectionStateChanged;
	}

	public void Refresh()
	{
		var device = _connectionService.ConnectedDevice;
		IsConnected = _connectionService.IsConnected;
		IsConnecting = _connectionService.IsConnecting;
		if (device is null)
		{
			Name = "Немає підключення";
			Id = string.Empty;
			ConnectionState = "Невідомо";
			ConnectedAtText = string.Empty;
			SetError(_connectionService.LastError);
			return;
		}

		Name = string.IsNullOrWhiteSpace(device.Name) ? "Невідомий пристрій" : device.Name;
		Id = device.Id.ToString();
		ConnectionState = device.State.ToString();
		ConnectedAtText = _connectionService.ConnectedAtUtc is null
			? string.Empty
			: $"Підключено: {_connectionService.ConnectedAtUtc:yyyy-MM-dd HH:mm:ss} UTC";
		SetError(null);
	}

	private void OnConnectionStateChanged(object? sender, EventArgs e)
	{
		Refresh();
	}

	private void SetError(string? message)
	{
		if (string.IsNullOrWhiteSpace(message))
		{
			ErrorText = string.Empty;
			HasError = false;
			return;
		}

		ErrorText = message;
		HasError = true;
	}

	private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
	{
		if (EqualityComparer<T>.Default.Equals(field, value))
			return;

		field = value;
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
