using System;
using System.Security.Authentication;
using System.Text.Json;
using System.Threading;
using AccidentMonitor.Application.common;
using InfluxDB.Client.Api.Domain;
using iot_monitor.Application.Enum;
using iot_monitor.Application.Interfaces;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Exceptions;

namespace iot_monitor.Infrastructure.MQTT
{
    public class MqttSettings
    {
        public string BrokerHost { get; set; } = "localhost";
        public int BrokerPort { get; set; } = 1883;
        public bool UseTls { get; set; } = false;
        public string ClientId { get; set; } = "iot-monitor-client";
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public int ConnectionTimeout { get; set; } = 10;
        public int KeepAliveInterval { get; set; } = 60;
        public int ProtocolVersion { get; set; } = 5;
        public string Protocol { get; set; } = "ws";
        public List<string> SubscribeTopics { get; set; } = new List<string> { "devices/+/data" };
        public string DeviceDataTopic { get; set; } = "devices/{deviceId}/data";
        public string CommandTopic { get; set; } = "devices/{deviceId}/command";
    }

    public class MqttClientService : IMqttService, IAsyncDisposable
    {
        private readonly ILogger<MqttClientService> _logger;
        private readonly MqttSettings _settings;
        private readonly IDeviceAnalysisService _deviceAnalysisService;
        private readonly IMqttClient _mqttClient;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private bool _isDisposed;
        private readonly MqttClientFactory _mqttFactory;
        private const int ReconnectDelaySeconds = 5;
        private readonly SemaphoreSlim _connectionLock = new(1, 1);
        public event Action<string, string> OnMessageReceived;
        public event Func<string, string, Task> OnMessageReceivedAsync;

        public MqttClientService(
            ILogger<MqttClientService> logger,
            IOptions<MqttSettings> settings,
            IDeviceAnalysisService deviceAnalysisService)
        {
            _logger = logger;
            _settings = settings.Value;
            _deviceAnalysisService = deviceAnalysisService;
            _mqttFactory = new MqttClientFactory();
            _mqttClient = _mqttFactory.CreateMqttClient();
            _cancellationTokenSource = new CancellationTokenSource();
            ConfigureClientHandlers();
        }

        private void ConfigureClientHandlers()
        {
            _mqttClient.DisconnectedAsync += HandleDisconnectionAsync;
            _mqttClient.ApplicationMessageReceivedAsync += HandleApplicationMessageReceivedAsync;
            _mqttClient.ConnectedAsync += args =>
            {
                _logger.LogInformation("MQTT Client Connected successfully.");
                return Task.CompletedTask;
            };
        }

        public async Task<ServiceResult> StartAsync(CancellationToken cancellationToken = default)
        {
            if (_isDisposed)
            {
                _logger.LogWarning("MQTT Client Service is disposed, cannot start.");
                return new ServiceResult(-1, "Service is disposed.");
            }
            _logger.LogInformation("Starting MQTT Client Service...");
            try
            {
                var result = await ConnectAsync(cancellationToken);
                if (!result.IsSuccess)
                {
                    return result;
                }

                return await SubscribeToConfiguredTopicsAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start MQTT service");
                return new ServiceResult(500, $"Failed to start MQTT service: {ex.Message}");
            }
        }

        public async Task<ServiceResult> ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (_isDisposed) return new ServiceResult(-1, "Service is disposed.");

            try
            {
                if (_mqttClient.IsConnected)
                {
                    return new ServiceResult(0, "Already connected to MQTT broker");
                }
                var uri = $"{_settings.BrokerHost}:{_settings.BrokerPort}/{_settings.Protocol}";
                var mqttClientOptions = new MqttClientOptionsBuilder()
                    .WithWebSocketServer(o => o.WithUri($"ws://{uri}"))
                    .WithClientId(_settings.ClientId)
                    .WithCleanSession()
                    .WithProtocolVersion(_settings.ProtocolVersion == 5
                    ? MQTTnet.Formatter.MqttProtocolVersion.V500
                    : MQTTnet.Formatter.MqttProtocolVersion.V311)
                    .WithTimeout(TimeSpan.FromSeconds(_settings.ConnectionTimeout))
                    .WithKeepAlivePeriod(TimeSpan.FromSeconds(_settings.KeepAliveInterval))
                    .WithCredentials(_settings.Username, _settings.Password);

                if (!string.IsNullOrEmpty(_settings.Username))
                {
                    mqttClientOptions.WithCredentials(_settings.Username, _settings.Password);
                }

                if (_settings.UseTls)
                {
                    var tlsOptions = new MqttClientTlsOptions
                    {
                        UseTls = true,
                        SslProtocol = SslProtocols.Tls12 | SslProtocols.Tls13,
                        CertificateValidationHandler = context =>
                        {
                            _logger.LogWarning("Custom TLS validation executed. SSL Policy Errors: {Errors}", context.SslPolicyErrors);
                            return true;
                        }
                    };

                    mqttClientOptions.WithTlsOptions(tlsOptions);
                }

                var result = await _mqttClient.ConnectAsync(mqttClientOptions.Build(), cancellationToken);

                if (result.ResultCode == MqttClientConnectResultCode.Success)
                {
                    _logger.LogInformation("Connected to MQTT broker at {Host}:{Port}", _settings.BrokerHost, _settings.BrokerPort);
                    return new ServiceResult(0, "Connected to MQTT broker successfully");
                }
                else
                {
                    _logger.LogError("Failed to connect to MQTT broker: {ResultCode}", result.ResultCode);
                    return new ServiceResult(500, $"Failed to connect to MQTT broker: {result.ResultCode}");
                }
            }
            catch (MqttCommunicationException ex)
            {
                _logger.LogError(ex, "MQTT connection error");
                return new ServiceResult(500, $"MQTT connection error: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error connecting to MQTT broker");
                return new ServiceResult(500, $"Unexpected error connecting to MQTT broker: {ex.Message}");
            }
        }

        public async Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            if (_mqttClient.IsConnected)
            {
                try
                {
                    await _mqttClient.DisconnectAsync(new MqttClientDisconnectOptions(), cancellationToken);
                    _logger.LogInformation("Disconnected from MQTT broker");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disconnecting from MQTT broker");
                }
            }
        }

        public async ValueTask DisposeAsync(CancellationToken cancellationToken = default)
        {
            if (!_mqttClient.IsConnected)
            {
                _logger.LogInformation("MQTT client is already disconnected.");
                return;
            }

            _logger.LogInformation("Disconnecting MQTT client...");
            try
            {
                var disconnectOptions = _mqttFactory.CreateClientDisconnectOptionsBuilder()
                    .WithReason(MqttClientDisconnectOptionsReason.NormalDisconnection)
                    .Build();
                await _mqttClient.DisconnectAsync(disconnectOptions, cancellationToken);
                _logger.LogInformation("MQTT client disconnected successfully.");
            }
            catch (MqttCommunicationException ex)
            {
                _logger.LogError(ex, "Communication error during disconnection.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred during disconnection.");
            }
        }

        public async Task<ServiceResult> PublishAsync<T>(
            string topic,
            T payloadDto,
            MQTTQoSLevel mqttQoSLevel = MQTTQoSLevel.AtLeastOnce,
            bool retain = false,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (!_mqttClient.IsConnected)
                {
                    var connectResult = await ConnectAsync(cancellationToken);
                    if (!connectResult.IsSuccess)
                    {
                        return connectResult;
                    }
                }

                var json = JsonSerializer.Serialize(payloadDto);
                var message = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(json)
                    .WithQualityOfServiceLevel((MQTTnet.Protocol.MqttQualityOfServiceLevel)mqttQoSLevel)
                    .WithRetainFlag(retain)
                    .Build();

                var result = await _mqttClient.PublishAsync(message, cancellationToken);

                _logger.LogInformation("Published message to topic {Topic}", topic);
                return new ServiceResult(0, "Message published successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing to topic {Topic}", topic);
                return new ServiceResult(500, $"Error publishing message: {ex.Message}");
            }
        }

        public async Task<ServiceResult> SubscribeToConfiguredTopicsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (!_mqttClient.IsConnected)
                {
                    var connectResult = await ConnectAsync(cancellationToken);
                    if (!connectResult.IsSuccess)
                    {
                        return connectResult;
                    }
                }

                var subscribeOptions = _mqttFactory.CreateSubscribeOptionsBuilder();

                foreach (var topic in _settings.SubscribeTopics)
                {
                    subscribeOptions.WithTopicFilter(f => f
                        .WithTopic(topic)
                        .WithQualityOfServiceLevel((MQTTnet.Protocol.MqttQualityOfServiceLevel)MQTTQoSLevel.AtLeastOnce));

                    _logger.LogInformation("Subscribing to topic: {Topic}", topic);
                }

                var result = await _mqttClient.SubscribeAsync(subscribeOptions.Build(), cancellationToken);

                bool allSuccess = true;
                foreach (var item in result.Items)
                {
                    if (item.ResultCode != MqttClientSubscribeResultCode.GrantedQoS0 &&
                        item.ResultCode != MqttClientSubscribeResultCode.GrantedQoS1 &&
                        item.ResultCode != MqttClientSubscribeResultCode.GrantedQoS2)
                    {
                        _logger.LogWarning("Failed to subscribe to topic {Topic}: {ResultCode}", item.TopicFilter.Topic, item.ResultCode);
                        allSuccess = false;
                    }
                    else
                    {
                        _logger.LogInformation("Successfully subscribed to topic {Topic}", item.TopicFilter.Topic);
                    }
                }

                if (allSuccess)
                {
                    return new ServiceResult(0, "Successfully subscribed to all topics");
                }
                else
                {
                    return new ServiceResult(500, "Failed to subscribe to one or more topics");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error subscribing to MQTT topics");
                return new ServiceResult(500, $"Error subscribing to topics: {ex.Message}");
            }
        }

        private async Task HandleApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs args)
        {
            try
            {
                string topic = args.ApplicationMessage.Topic;
                string payload = args.ApplicationMessage.ConvertPayloadToString();

                _logger.LogInformation("Received message on topic {Topic}: {Payload}", topic, payload);

                string deviceId = ExtractDeviceIdFromTopic(topic);
                if (string.IsNullOrEmpty(deviceId))
                {
                    _logger.LogWarning("Could not extract device ID from topic {Topic}", topic);
                    return;
                }

                OnMessageReceived?.Invoke(topic, payload);
                if (OnMessageReceivedAsync != null)
                {
                    await OnMessageReceivedAsync.Invoke(topic, payload);
                }

                if (IsDeviceDataTopic(topic))
                {
                    await ProcessDeviceDataAsync(deviceId, payload);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling MQTT message");
            }
        }

        private static string ExtractDeviceIdFromTopic(string topic)
        {
            var parts = topic.Split('/');
            if (parts.Length >= 3 && parts[0] == "devices")
            {
                return parts[1];
            }
            return string.Empty;
        }

        private static bool IsDeviceDataTopic(string topic)
        {
            return topic.StartsWith("devices/") && topic.EndsWith("/data");
        }

        private async Task ProcessDeviceDataAsync(string deviceId, string payload)
        {
            try
            {
                var deviceData = JsonSerializer.Deserialize<Dictionary<string, object>>(payload,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (deviceData == null)
                {
                    _logger.LogWarning("Failed to deserialize device data for device {DeviceId}", deviceId);
                    return;
                }

                var analysisResult = await _deviceAnalysisService.AnalyzeDeviceDataAsync(deviceId, deviceData);

                await _deviceAnalysisService.SaveAnalysisHistoryAsync(analysisResult);

                _logger.LogInformation("Processed data for device {DeviceId}, status: {Status}",
                    deviceId, analysisResult.Status);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Error parsing device data JSON for device {DeviceId}", deviceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing device data for device {DeviceId}", deviceId);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore();
            Dispose(false);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed) return;

            if (disposing)
            {
                try
                {
                    _connectionLock.Wait(TimeSpan.FromSeconds(5));
                    DisposeAsyncCore().AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during synchronous disposal of async resources.");
                }
                finally
                {
                    // TODO Release the semaphore if it was acquired
                }
            }

            _isDisposed = true;
        }

        private async ValueTask DisposeAsyncCore()
        {
            if (_isDisposed) return;

            _logger.LogInformation("Disposing MQTT Client Service...");
            _isDisposed = true;

            await _connectionLock.WaitAsync();
            try
            {
                if (!_cancellationTokenSource.IsCancellationRequested)
                {
                    _logger.LogDebug("Canceling internal operations...");
                    _cancellationTokenSource.Cancel();
                }

                _mqttClient.DisconnectedAsync -= HandleDisconnectionAsync;
                _mqttClient.ApplicationMessageReceivedAsync -= HandleApplicationMessageReceivedAsync;
                _mqttClient.ConnectedAsync -= null;

                if (_mqttClient.IsConnected)
                {
                    _logger.LogInformation("Disconnecting MQTT client during disposal...");
                    try
                    {
                        var disconnectOptions = _mqttFactory.CreateClientDisconnectOptionsBuilder()
                                                .WithReason(MqttClientDisconnectOptionsReason.NormalDisconnection)
                                                .Build();
                        await _mqttClient.DisconnectAsync(disconnectOptions, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
                        _logger.LogInformation("MQTT client disconnected during disposal.");
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogWarning("Disconnection during disposal timed out.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error disconnecting MQTT client during disposal. Proceeding with disposal.");
                    }
                }
                else
                {
                    _logger.LogDebug("MQTT client already disconnected during disposal.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during initial phase of disposal.");
            }
            finally
            {
                _cancellationTokenSource?.Dispose();

                _mqttClient?.Dispose();

                _connectionLock?.Release();
                _connectionLock?.Dispose();

                _logger.LogInformation("MQTT Client Service disposed.");
            }
        }

        private async Task HandleDisconnectionAsync(MqttClientDisconnectedEventArgs args)
        {
            if (_isDisposed || args.Reason == MqttClientDisconnectReason.NormalDisconnection)
            {
                _logger.LogInformation("MQTT client disconnected. Reason: {Reason}. Reconnection not attempted.", args.Reason);
                return;
            }

            _logger.LogWarning("Disconnected from MQTT broker. Reason: {Reason}. Client Was Connected: {WasConnected}. Attempting to reconnect in {Delay} seconds...",
                args.Reason, args.ClientWasConnected, ReconnectDelaySeconds);

            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(ReconnectDelaySeconds), _cancellationTokenSource.Token);

                    _logger.LogInformation("Attempting to reconnect...");
                    var connectResult = await ConnectAsync(_cancellationTokenSource.Token);

                    if (connectResult.Code == 0 && _mqttClient.IsConnected)
                    {
                        _logger.LogInformation("Successfully reconnected to MQTT broker.");
                        var subscribeResult = await SubscribeToConfiguredTopicsAsync(_cancellationTokenSource.Token);
                        if (subscribeResult.Code != 0)
                        {
                            _logger.LogWarning("Reconnected, but failed to re-subscribe to topics. Result: {Code} - {Message}", subscribeResult.Code, subscribeResult.Message);
                        }
                        else
                        {
                            _logger.LogInformation("Successfully re-subscribed to topics after reconnection.");
                        }
                        break;
                    }
                    else
                    {
                        _logger.LogWarning("Reconnect attempt failed. Result: {Code} - {Message}. Retrying after {Delay} seconds...",
                                           connectResult.Code, connectResult.Message, ReconnectDelaySeconds);
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Reconnection loop canceled.");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error during reconnection attempt. Retrying after {Delay} seconds...", ReconnectDelaySeconds);
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(ReconnectDelaySeconds), _cancellationTokenSource.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInformation("Reconnection loop canceled while handling unexpected error.");
                        break;
                    }
                }
            }
            if (_cancellationTokenSource.IsCancellationRequested)
            {
                _logger.LogInformation("Reconnection attempts stopped because the service is disposing.");
            }
        }
    }
}
