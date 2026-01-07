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
	private string _commandText = string.Empty;
	private string _statsText = string.Empty;
	private bool _hasError;
	private bool _isConnected;
	private bool _isConnecting;

	public event PropertyChangedEventHandler? PropertyChanged;

	public Command DisconnectCommand { get; }
	public Command SendCommandCommand { get; }

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

	public string CommandText
	{
		get => _commandText;
		set
		{
			if (SetField(ref _commandText, value))
				OnPropertyChanged(nameof(CanSend));
		}
	}

	public string StatsText
	{
		get => _statsText;
		private set => SetField(ref _statsText, value);
	}

	public bool CanSend => IsConnected && !IsConnecting && !string.IsNullOrWhiteSpace(CommandText);

	public bool HasError
	{
		get => _hasError;
		private set => SetField(ref _hasError, value);
	}

	public bool IsConnected
	{
		get => _isConnected;
		private set
		{
			if (SetField(ref _isConnected, value))
				OnPropertyChanged(nameof(CanSend));
		}
	}

	public bool IsConnecting
	{
		get => _isConnecting;
		private set
		{
			if (SetField(ref _isConnecting, value))
				OnPropertyChanged(nameof(CanSend));
		}
	}

	public DeviceDetailViewModel(DeviceConnectionService connectionService)
	{
		_connectionService = connectionService;
		_connectionService.ConnectionStateChanged += OnConnectionStateChanged;
		_connectionService.StatsChanged += OnStatsChanged;
		DisconnectCommand = new Command(async () => await _connectionService.DisconnectAsync());
		SendCommandCommand = new Command(async () => await SendCommandAsync());
		Refresh();
	}

	public void Dispose()
	{
		_connectionService.ConnectionStateChanged -= OnConnectionStateChanged;
		_connectionService.StatsChanged -= OnStatsChanged;
	}

	public void Refresh()
	{
		var device = _connectionService.ConnectedDevice;
		IsConnected = _connectionService.IsConnected;
		IsConnecting = _connectionService.IsConnecting;
		StatsText = _connectionService.StatsText;
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

	private void OnStatsChanged(object? sender, EventArgs e)
	{
		StatsText = _connectionService.StatsText;
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

	private async Task SendCommandAsync()
	{
		var command = CommandText;
		if (string.IsNullOrWhiteSpace(command))
			return;

		var sent = await _connectionService.SendCommandAsync(command);
		Refresh();

		if (sent)
			CommandText = string.Empty;
	}

	private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
	{
		if (EqualityComparer<T>.Default.Equals(field, value))
			return false;

		field = value;
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		return true;
	}

	private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
