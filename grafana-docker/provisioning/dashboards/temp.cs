using System.Security.Authentication;
using System.Text.Json;
using AccidentMonitor.Application.common;
using iot_monitor.Application.Enum;
using iot_monitor.Application.Interfaces;
using iot_monitor.Infrastructure.MQTT;
using MQTTnet;
using MQTTnet.Adapter;
using MQTTnet.Exceptions;
using MQTTnet.Formatter;
using MQTTnet.Protocol;

namespace AccidentMonitor.Infrastructure.MQTT
{
    public class MqttClientService : IMqttService, IAsyncDisposable, IDisposable
    {
        private readonly IMqttClient _mqttClient;
        private readonly ILogger<MqttClientService> _logger;
        private readonly MqttConnectionConfiguration _config;
        private readonly MqttClientFactory _mqttClientFactory;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private bool _isDisposed;
        private readonly SemaphoreSlim _connectionLock = new(1, 1);

        public event Func<string, string, Task>? OnMessageReceivedAsync;

        private const int ReconnectDelaySeconds = 5;

        public event Action<string, string>? OnMessageReceived;

        public MqttClientService(
            MqttConnectionConfiguration options,
            ILogger<MqttClientService> logger)
        {
            _config = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _mqttClientFactory = new MqttClientFactory();
            _mqttClient = _mqttClientFactory.CreateMqttClient();
            _cancellationTokenSource = new CancellationTokenSource();

            ConfigureClientHandlers();
        }

        private void ConfigureClientHandlers()
        {
            _mqttClient.DisconnectedAsync += HandleDisconnectionAsync;
            _mqttClient.ApplicationMessageReceivedAsync += HandleMessageAsync;
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
            var connectResult = await ConnectAsync(cancellationToken);

            if (connectResult.Code != 0)
            {
                _logger.LogError("Failed to connect during startup. Result: {ResultCode} - {Message}", connectResult.Code, connectResult.Message);
                return new ServiceResult(-1, $"Failed to start MQTT client: {connectResult.Message}");
            }

            var subscribeResult = await SubscribeToConfiguredTopicsAsync(cancellationToken);
            if (subscribeResult.Code != 0)
            {
                _logger.LogError("Failed to subscribe to topics during startup. Result: {ResultCode} - {Message}", subscribeResult.Code, subscribeResult.Message);
                return new ServiceResult(-1, $"MQTT Client started but failed to subscribe: {subscribeResult.Message}");
            }

            _logger.LogInformation("MQTT Client started and subscribed successfully.");
            return new ServiceResult(0, "MQTT Client started successfully.");
        }

        private MqttClientOptions BuildMqttClientOptions()
        {
            var builder = _mqttClientFactory.CreateClientOptionsBuilder()
               .WithClientId(_config.ClientID)
               .WithProtocolVersion((MqttProtocolVersion)_config.ProtocolVersion)
               .WithTimeout(TimeSpan.FromSeconds(10)) 
               .WithKeepAlivePeriod(TimeSpan.FromSeconds(_config.KeepAlivePeriod > 0 ? _config.KeepAlivePeriod : 60))
               .WithCleanSession(_config.CleanSession)
               .WithCredentials(_config.Username, _config.Password);

            // WebSocket or TCP
            if (_config.UseWebSocket)
            {
                var wsUri = $"ws://{_config.Broker}:{_config.Port}/{_config.Protocol}";
                if (_config.UseTls) wsUri = $"wss://{_config.Broker}:{_config.Port}/{_config.Protocol}";

                _logger.LogInformation("Configuring WebSocket connection to {Uri}", wsUri);
                builder = builder.WithWebSocketServer(o => o.WithUri(wsUri));
            }
            else
            {
                _logger.LogInformation("Configuring TCP connection to {Broker}:{Port}", _config.Broker, _config.Port);
                builder = builder.WithTcpServer(_config.Broker, _config.Port);
            }

            if (_config.UseTls)
            {
                _logger.LogInformation("Configuring TLS options.");
                try
                {
                    var tlsOptions = new MqttClientTlsOptions
                    {
                        UseTls = true,
                        SslProtocol = SslProtocols.Tls12 | SslProtocols.Tls13,
                        AllowUntrustedCertificates = _config.AllowUntrustedCertificates,
                        IgnoreCertificateChainErrors = _config.IgnoreCertificateChainErrors,
                        IgnoreCertificateRevocationErrors = _config.IgnoreCertificateRevocationErrors,

                        CertificateValidationHandler = context =>
                        {
                            _logger.LogWarning("Custom TLS validation executed. SSL Policy Errors: {Errors}", context.SslPolicyErrors);
                            return true;
                        }
                    };

                    //if (!string.IsNullOrWhiteSpace(_config.ClientCertPath)) {
                    // var clientCerts = LoadClientCertificates(_config.ClientCertPath, _config.ClientCertPassword);
                    // tlsOptions.ClientCertificatesProvider = new DefaultMqttCertificatesProvider(clientCerts);
                    //}

                    builder = builder.WithTlsOptions(tlsOptions);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed configure TLS options. Check TLS settings and certificate paths/permissions.");
                    throw new InvalidOperationException("Failed to configure MQTT TLS options.", ex);
                }
            }

            return builder.Build();
        }

        public async Task<ServiceResult> ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (_isDisposed) return new ServiceResult(-1, "Service is disposed.");
            if (_mqttClient.IsConnected) return new ServiceResult(0, "Already connected.");

            await _connectionLock.WaitAsync(cancellationToken);
            try
            {
                if (_isDisposed) return new ServiceResult(-1, "Service is disposed while waiting for connection lock.");
                if (_mqttClient.IsConnected) return new ServiceResult(0, "Already connected.");
                _logger.LogInformation("Attempting to connect to MQTT broker at {Server}:{Port}...",
                                       _config.Broker, _config.Port);

                MqttClientOptions clientOptions;
                try
                {
                    clientOptions = BuildMqttClientOptions();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to build MQTT client options.");
                    return new ServiceResult(-1, $"Configuration error: {ex.Message}");
                }

                MqttClientConnectResult response;
                try
                {
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cancellationTokenSource.Token);
                    response = await _mqttClient.ConnectAsync(clientOptions, linkedCts.Token);

                    if (response.ResultCode == MqttClientConnectResultCode.Success)
                    {
                        _logger.LogInformation("Connected to MQTT broker successfully. ResultCode: {ResultCode}", response.ResultCode);
                        return new ServiceResult(0, "Connection successful.");
                    }
                    else
                    {
                        _logger.LogWarning("Failed to connect to MQTT broker. ResultCode: {ResultCode}, Reason: {Reason}",
                                          response.ResultCode, response.ReasonString);
                        return new ServiceResult((int)response.ResultCode, $"Connection failed: {response.ResultCode} - {response.ReasonString}");
                    }
                }
                catch (MqttConnectingFailedException ex)
                {
                    _logger.LogError(ex, $"Authentication or connection setup failed when connecting to MQTT broker. Error message: {ex.Message}. Verify credentials, network, and broker status.", ex.Message);
                    return new ServiceResult((int)MqttClientConnectResultCode.UnspecifiedError, $"Connection failed: {ex.Message}");
                }
                catch (MqttCommunicationException ex)
                {
                    _logger.LogError(ex, "Communication error while connecting to MQTT broker.");
                    return new ServiceResult(-1, $"Communication error: {ex.Message}");
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Connection attempt was canceled.");
                    return new ServiceResult(-1, "Connection attempt canceled.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An unexpected error occurred while connecting to MQTT broker.");
                    return new ServiceResult(-1, $"Unexpected connection error: {ex.Message}");
                }
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        public async Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            if (!_mqttClient.IsConnected)
            {
                _logger.LogInformation("MQTT client is already disconnected.");
                return;
            }

            _logger.LogInformation("Disconnecting MQTT client...");
            try
            {
                var disconnectOptions = _mqttClientFactory.CreateClientDisconnectOptionsBuilder()
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

        public async Task<ServiceResult> SubscribeToConfiguredTopicsAsync(CancellationToken cancellationToken = default)
        {
            if (!_mqttClient.IsConnected)
            {
                _logger.LogWarning("Cannot subscribe, MQTT client is not connected.");
                return new ServiceResult(-1, "Client not connected.");
            }
            if (_config.SubTopics == null || _config.SubTopics.Length == 0)
            {
                _logger.LogInformation("No topics configured for subscription.");
                return new ServiceResult(0, "No topics to subscribe.");
            }
            try
            {
                var subscribeOptionsBuilder = _mqttClientFactory.CreateSubscribeOptionsBuilder();
                foreach (var topic in _config.SubTopics)
                {
                    if (string.IsNullOrWhiteSpace(topic)) continue;

                    var topicFilter = topic.Contains("{id}") ? topic.Replace("{id}", "+") : topic;
                    topicFilter = topicFilter.Contains("{sensor}") ? topicFilter.Replace("{sensor}", "#") : topicFilter;

                    var qos = (MqttQualityOfServiceLevel)(_config.QoS >= 0 && _config.QoS <= 2 ? _config.QoS : (int)MqttQualityOfServiceLevel.AtLeastOnce);

                    _logger.LogInformation("Adding subscription: Topic='{TopicFilter}', QoS={QoS}", topicFilter, qos);
                    subscribeOptionsBuilder.WithTopicFilter(topicFilter, qos);
                }

                var subscribeOptions = subscribeOptionsBuilder.Build();

                if (subscribeOptions.TopicFilters.Count == 0)
                {
                    _logger.LogInformation("No valid topics found to subscribe to after filtering.");
                    return new ServiceResult(0, "No valid topics to subscribe.");
                }

                _logger.LogInformation("Subscribing to {Count} topic filters...", subscribeOptions.TopicFilters.Count);
                var response = await _mqttClient.SubscribeAsync(subscribeOptions, cancellationToken);

                bool allSuccess = true;
                foreach (var resultItem in response.Items)
                {
                    if (resultItem.ResultCode == MqttClientSubscribeResultCode.GrantedQoS0 ||
                       resultItem.ResultCode == MqttClientSubscribeResultCode.GrantedQoS1 ||
                       resultItem.ResultCode == MqttClientSubscribeResultCode.GrantedQoS2)
                    {
                        _logger.LogInformation("Successfully subscribed to Topic: '{Topic}', Granted QoS: {QoS}",
                                              resultItem.TopicFilter.Topic, resultItem.ResultCode);
                    }
                    else
                    {
                        allSuccess = false;
                        _logger.LogError("Failed to subscribe to Topic: '{Topic}', Reason: {ReasonCode}",
                                         resultItem.TopicFilter.Topic, resultItem.ResultCode);
                    }
                }
                if (allSuccess)
                {
                    return new ServiceResult(0, "Subscribed to all configured topics successfully.");
                }
                else
                {
                    return new ServiceResult(-1, "Failed to subscribe to one or more topics. Check logs for details.");
                }
            }
            catch (MqttCommunicationException ex)
            {
                _logger.LogError(ex, "Communication error while subscribing to topics.");
                return new ServiceResult(-1, $"Subscribe failed due to communication error: {ex.Message}");
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Subscription operation was canceled.");
                return new ServiceResult(-1, "Subscription canceled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while subscribing to topics.");
                return new ServiceResult(-1, $"Subscribe failed: {ex.Message}");
            }
        }

        public async Task<ServiceResult> UnsubscribeAsync(string topic, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(topic))
            {
                return new ServiceResult(-1, "Topic cannot be null or whitespace.");
            }
            if (!_mqttClient.IsConnected)
            {
                _logger.LogWarning("Cannot unsubscribe, MQTT client is not connected.");
                return new ServiceResult(-1, "Client not connected.");
            }

            try
            {
                _logger.LogInformation("Unsubscribing from topic: {Topic}", topic);
                var unsubscribeOptions = _mqttClientFactory.CreateUnsubscribeOptionsBuilder()
                    .WithTopicFilter(topic)
                    .Build();

                var response = await _mqttClient.UnsubscribeAsync(unsubscribeOptions, cancellationToken);

                bool allSuccess = true;
                foreach (var item in response.Items)
                {
                    if (item.ResultCode != MqttClientUnsubscribeResultCode.Success)
                    {
                        allSuccess = false;
                        _logger.LogError("Failed to unsubscribe from Topic: '{Topic}', Reason: {ReasonCode}",
                                         item.TopicFilter, item.ResultCode);
                    }
                }

                if (allSuccess)
                {
                    _logger.LogInformation("Successfully unsubscribed from topic: {Topic}", topic);
                    return new ServiceResult(0, $"Unsubscribed from topic {topic}");
                }
                else
                {
                    return new ServiceResult(-1, $"Failed to fully unsubscribe from topic {topic}. Check logs.");
                }

            }
            catch (MqttCommunicationException ex)
            {
                _logger.LogError(ex, "Communication error while unsubscribing from topic {Topic}", topic);
                return new ServiceResult(-1, $"Unsubscribe failed due to communication error: {ex.Message}");
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Unsubscribe operation for topic {Topic} was canceled.", topic);
                return new ServiceResult(-1, "Unsubscribe canceled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while unsubscribing from topic {Topic}", topic);
                return new ServiceResult(-1, $"Unsubscribe failed: {ex.Message}");
            }
        }

        private Task HandleMessageAsync(MqttApplicationMessageReceivedEventArgs args)
        {
            if (_isDisposed)
                return Task.CompletedTask;
            var topic = args.ApplicationMessage.Topic;
            var payload = args.ApplicationMessage.ConvertPayloadToString();

            if (_cancellationTokenSource.IsCancellationRequested)
            {
                _logger.LogWarning("Cancellation requested during message handling for topic {Topic}. Skipping invocation.", topic);
                args.ProcessingFailed = true;
                return Task.CompletedTask;
            }

            _logger.LogDebug("Received message - Topic: '{Topic}', QoS: {QoS}, Retain: {Retain}, Payload: {Payload}",
                             topic, args.ApplicationMessage.QualityOfServiceLevel, args.ApplicationMessage.Retain, payload);

            try
            {
                _logger.LogDebug("Received message - Topic: '{Topic}', QoS: {QoS}, Retain: {Retain}, Payload: {Payload}",
                               topic, args.ApplicationMessage.QualityOfServiceLevel, args.ApplicationMessage.Retain, payload);

                if (OnMessageReceivedAsync != null)
                {
                    try
                    {
                        _ = Task.Run(() => OnMessageReceivedAsync(topic, payload))
                             .ContinueWith(t =>
                             {
                                 if (t.IsFaulted && t.Exception != null)
                                 {
                                     _logger.LogError(t.Exception, "Error executing async message handler task for topic {Topic}", topic);
                                 }
                             });
                    }
                    catch (Exception asyncHandlerEx)
                    {
                        _logger.LogError(asyncHandlerEx, "Error starting async message handler task for topic {Topic}", topic);
                        args.ProcessingFailed = true;
                        return Task.CompletedTask;
                    }
                }
                args.IsHandled = true;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Message handling canceled for topic {Topic}", args.ApplicationMessage?.Topic ?? "unknown");
                args.ProcessingFailed = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing received MQTT message on topic {Topic}", args.ApplicationMessage?.Topic ?? "unknown");
                args.ProcessingFailed = true;
            }

            return Task.CompletedTask;
        }

        public static class MqttQoSLevelConverter
        {
            public static MqttQualityOfServiceLevel ConvertToMqttQualityOfServiceLevel(MQTTQoSLevel qosLevel)
            {
                return qosLevel switch
                {
                    MQTTQoSLevel.AtMostOnce => MqttQualityOfServiceLevel.AtMostOnce,
                    MQTTQoSLevel.AtLeastOnce => MqttQualityOfServiceLevel.AtLeastOnce,
                    MQTTQoSLevel.ExactlyOnce => MqttQualityOfServiceLevel.ExactlyOnce,
                    _ => throw new ArgumentOutOfRangeException(nameof(qosLevel), qosLevel, null)
                };
            }
        }

        public async Task<ServiceResult> PublishAsync<T>(
            string topic, 
            T payloadDto,
            MQTTQoSLevel mqttQoSLevel = MQTTQoSLevel.AtLeastOnce, 
            bool retain = false, 
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(topic)) return new ServiceResult(-1, "Topic cannot be null or whitespace.");
            if (payloadDto == null) return new ServiceResult(-1, "Payload cannot be null.");
            if (!_mqttClient.IsConnected)
            {
                _logger.LogWarning("Cannot publish to topic {Topic}, MQTT client is not connected.", topic);
                return new ServiceResult(-1, "Client not connected.");
            }
            var qosLevel = MqttQoSLevelConverter.ConvertToMqttQualityOfServiceLevel(mqttQoSLevel);
            try
            {
                string jsonPayload;
                try
                {
                    jsonPayload = JsonSerializer.Serialize(payloadDto);
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogError(jsonEx, "Failed to serialize payload DTO for topic {Topic}", topic);
                    return new ServiceResult(-1, $"Payload serialization failed: {jsonEx.Message}");
                }
                catch (NotSupportedException nse)
                {
                    _logger.LogError(nse, "Payload DTO type not supported for JSON serialization for topic {Topic}", topic);
                    return new ServiceResult(-1, $"Payload serialization failed: {nse.Message}");
                }

                var message = _mqttClientFactory.CreateApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(jsonPayload)
                    .WithQualityOfServiceLevel(qosLevel)
                    .WithRetainFlag(retain) 
                    .Build();

                _logger.LogDebug("Publishing message - Topic: '{Topic}', QoS: {QoS}, Retain: {Retain}, Payload: {Payload}",
                               topic, qosLevel, retain, jsonPayload);

                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cancellationTokenSource.Token);
                var response = await _mqttClient.PublishAsync(message, linkedCts.Token);

                if (response.ReasonCode == MqttClientPublishReasonCode.Success)
                {
                    _logger.LogInformation("Successfully published message to topic {Topic}", topic);
                    return new ServiceResult(0, response.ReasonString ?? "Publish successful.");
                }
                else
                {
                    _logger.LogError("Failed to publish message to topic {Topic}. Reason Code: {ReasonCode}, Reason String: {ReasonString}",
                                    topic, response.ReasonCode, response.ReasonString);
                    return new ServiceResult((int)response.ReasonCode, $"Publish failed: {response.ReasonCode} - {response.ReasonString}");
                }

            }
            catch (MqttCommunicationException ex)
            {
                _logger.LogError(ex, "Communication error while publishing to topic {Topic}", topic);
                return new ServiceResult(-1, $"Publish failed due to communication error: {ex.Message}");
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Publish operation to topic {Topic} was canceled.", topic);
                return new ServiceResult(-1, "Publish canceled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while publishing to topic {Topic}", topic);
                return new ServiceResult(-1, $"Publish failed: {ex.Message}");
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
                _mqttClient.ApplicationMessageReceivedAsync -= HandleMessageAsync;
                _mqttClient.ConnectedAsync -= null;

                if (_mqttClient.IsConnected)
                {
                    _logger.LogInformation("Disconnecting MQTT client during disposal...");
                    try
                    {
                        var disconnectOptions = _mqttClientFactory.CreateClientDisconnectOptionsBuilder()
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
    }
}