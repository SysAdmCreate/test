using System;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;

namespace test.Services;

public sealed class DeviceConnectionService
{
	private static readonly Guid s_serviceId = Guid.Parse("9b2a1c50-4f66-4c3e-9a6b-6f0c6b2f3a01");
	private static readonly Guid s_commandCharacteristicId = Guid.Parse("9b2a1c50-4f66-4c3e-9a6b-6f0c6b2f3a02");

	private readonly object _sync = new();
	private CancellationTokenSource? _reconnectCts;
	private IAdapter? _adapter;
	private bool _manualDisconnect;
	private bool _isConnecting;
	private ICharacteristic? _commandCharacteristic;
	private bool _isNotifying;
	private readonly ThroughputTracker _throughput = new();

	public event EventHandler? ConnectionStateChanged;
	public event EventHandler? StatsChanged;

	public IDevice? ConnectedDevice { get; private set; }
	public DateTime? ConnectedAtUtc { get; private set; }
	public string? LastError { get; private set; }
	public bool IsConnected => ConnectedDevice is not null;
	public bool IsConnecting => _isConnecting;
	public string StatsText => _throughput.GetSummary();

	public async Task<bool> ConnectAndMaintainAsync(IAdapter adapter, IDevice device)
	{
		lock (_sync)
		{
			_adapter = adapter;
			_manualDisconnect = false;
		}

		_adapter.DeviceDisconnected -= OnDeviceDisconnected;
		_adapter.DeviceDisconnected += OnDeviceDisconnected;

		var connected = await ConnectOnceAsync(device);
		if (connected)
		{
			StartReconnectLoop(device);
		}

		return connected;
	}

	public async Task DisconnectAsync()
	{
		IDevice? device;
		IAdapter? adapter;

		lock (_sync)
		{
			_manualDisconnect = true;
			device = ConnectedDevice;
			adapter = _adapter;
		}

		_reconnectCts?.Cancel();
		_reconnectCts = null;

		if (adapter is not null)
			adapter.DeviceDisconnected -= OnDeviceDisconnected;

		await StopNotificationsAsync();

		if (device is not null && adapter is not null)
		{
			try
			{
				await adapter.DisconnectDeviceAsync(device);
			}
			catch
			{
				// Ignore disconnect errors.
			}
		}

		ConnectedDevice = null;
		ConnectedAtUtc = null;
		LastError = null;
		_commandCharacteristic = null;
		_isNotifying = false;
		_throughput.Reset();
		OnConnectionStateChanged();
	}

	private async Task<bool> ConnectOnceAsync(IDevice device)
	{
		try
		{
			_isConnecting = true;
			OnConnectionStateChanged();
			LastError = null;
			await _adapter!.ConnectToDeviceAsync(device);
			ConnectedDevice = device;
			ConnectedAtUtc = DateTime.UtcNow;
			_commandCharacteristic = null;
			_isNotifying = false;
			_throughput.Reset();
			await GetCommandCharacteristicAsync(device);
			_isConnecting = false;
			OnConnectionStateChanged();
			return true;
		}
		catch (Exception ex)
		{
			ConnectedDevice = null;
			ConnectedAtUtc = null;
			LastError = ex.Message;
			_isConnecting = false;
			_throughput.Reset();
			OnConnectionStateChanged();
			return false;
		}
	}

	private void StartReconnectLoop(IDevice device)
	{
		lock (_sync)
		{
			if (_manualDisconnect)
				return;

			if (_reconnectCts is not null)
				return;

			_reconnectCts = new CancellationTokenSource();
		}

		_ = Task.Run(() => ReconnectLoopAsync(device, _reconnectCts.Token));
	}

	private async Task ReconnectLoopAsync(IDevice device, CancellationToken token)
	{
		while (!token.IsCancellationRequested)
		{
			if (_manualDisconnect)
				return;

			if (ConnectedDevice is not null)
			{
				await Task.Delay(TimeSpan.FromSeconds(2), token);
				continue;
			}

			await ConnectOnceAsync(device);
			await Task.Delay(TimeSpan.FromSeconds(3), token);
		}
	}

	private void OnDeviceDisconnected(object? sender, Plugin.BLE.Abstractions.EventArgs.DeviceEventArgs e)
	{
		if (_manualDisconnect)
			return;

		if (ConnectedDevice is not null && e.Device.Id == ConnectedDevice.Id)
		{
			ConnectedDevice = null;
			ConnectedAtUtc = null;
			if (_commandCharacteristic is not null)
				_commandCharacteristic.ValueUpdated -= OnCharacteristicValueUpdated;
			_commandCharacteristic = null;
			_isNotifying = false;
			_throughput.Reset();
			OnConnectionStateChanged();
			StartReconnectLoop(e.Device);
		}
	}

	public async Task<bool> SendCommandAsync(string command)
	{
		if (string.IsNullOrWhiteSpace(command))
		{
			LastError = "Команда порожня.";
			OnConnectionStateChanged();
			return false;
		}

		IDevice? device;
		lock (_sync)
		{
			device = ConnectedDevice;
		}

		if (device is null)
		{
			LastError = "Немає підключення.";
			OnConnectionStateChanged();
			return false;
		}

		try
		{
			var characteristic = await GetCommandCharacteristicAsync(device);
			if (characteristic is null)
			{
				LastError = "BLE характеристика не знайдена.";
				OnConnectionStateChanged();
				return false;
			}

			if (!characteristic.CanWrite)
			{
				LastError = "Характеристика не підтримує запис.";
				OnConnectionStateChanged();
				return false;
			}

			var payload = Encoding.UTF8.GetBytes(command.Trim());
			await characteristic.WriteAsync(payload);
			_throughput.AddTx(payload.Length);
			OnStatsChanged();
			LastError = null;
			OnConnectionStateChanged();
			return true;
		}
		catch (Exception ex)
		{
			LastError = ex.Message;
			OnConnectionStateChanged();
			return false;
		}
	}

	private async Task<ICharacteristic?> GetCommandCharacteristicAsync(IDevice device)
	{
		if (_commandCharacteristic is not null)
			return _commandCharacteristic;

		var service = await device.GetServiceAsync(s_serviceId);
		if (service is null)
			return null;

		_commandCharacteristic = await service.GetCharacteristicAsync(s_commandCharacteristicId);
		if (_commandCharacteristic is not null)
			await EnsureNotificationsAsync(_commandCharacteristic);
		return _commandCharacteristic;
	}

	private async Task EnsureNotificationsAsync(ICharacteristic characteristic)
	{
		if (_isNotifying || !characteristic.CanUpdate)
			return;

		characteristic.ValueUpdated += OnCharacteristicValueUpdated;
		await characteristic.StartUpdatesAsync();
		_isNotifying = true;
	}

	private async Task StopNotificationsAsync()
	{
		if (_commandCharacteristic is null)
			return;

		if (_isNotifying && _commandCharacteristic.CanUpdate)
		{
			_commandCharacteristic.ValueUpdated -= OnCharacteristicValueUpdated;
			await _commandCharacteristic.StopUpdatesAsync();
		}

		_isNotifying = false;
	}

	private void OnCharacteristicValueUpdated(object? sender, CharacteristicUpdatedEventArgs e)
	{
		var value = e.Characteristic?.Value;
		if (value is null)
			return;

		_throughput.AddRx(value.Length);
		OnStatsChanged();
	}

	private void OnConnectionStateChanged()
	{
		ConnectionStateChanged?.Invoke(this, EventArgs.Empty);
	}

	private void OnStatsChanged()
	{
		StatsChanged?.Invoke(this, EventArgs.Empty);
	}

	private sealed class ThroughputTracker
	{
		private long _totalTxBytes;
		private long _totalRxBytes;
		private DateTime _startUtc;

		public ThroughputTracker()
		{
			Reset();
		}

		public void Reset()
		{
			_totalTxBytes = 0;
			_totalRxBytes = 0;
			_startUtc = DateTime.UtcNow;
		}

		public void AddTx(int bytes)
		{
			if (bytes <= 0)
				return;

			_totalTxBytes += bytes;
		}

		public void AddRx(int bytes)
		{
			if (bytes <= 0)
				return;

			_totalRxBytes += bytes;
		}

		public string GetSummary()
		{
			var elapsedSeconds = (DateTime.UtcNow - _startUtc).TotalSeconds;
			if (elapsedSeconds <= 0.01)
				elapsedSeconds = 0.01;

			var txRate = _totalTxBytes / elapsedSeconds;
			var rxRate = _totalRxBytes / elapsedSeconds;

			return $"TX: {_totalTxBytes} B ({txRate:F1} B/s) | RX: {_totalRxBytes} B ({rxRate:F1} B/s)";
		}
	}
}
