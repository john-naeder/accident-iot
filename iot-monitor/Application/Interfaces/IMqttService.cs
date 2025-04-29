using AccidentMonitor.Application.common;
using iot_monitor.Application.Enum;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace iot_monitor.Application.Interfaces
{
    public interface IMqttService
    {
        event Action<string, string> OnMessageReceived;
        event Func<string, string, Task> OnMessageReceivedAsync;

        /// <summary>
        /// Start the MQTT service and connect to the broker
        /// </summary>
        Task<ServiceResult> StartAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Connect to the MQTT broker
        /// </summary>
        Task<ServiceResult> ConnectAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Disconnect from the MQTT broker
        /// </summary>
        Task DisconnectAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Subscribe to configured topics
        /// </summary>
        Task<ServiceResult> SubscribeToConfiguredTopicsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Publish a message to a topic
        /// </summary>
        /// <typeparam name="T">Type of the payload</typeparam>
        /// <param name="topic">MQTT topic</param>
        /// <param name="payloadDto">Message payload</param>
        /// <param name="mqttQoSLevel">QoS level</param>
        /// <param name="retain">Whether to retain the message</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task<ServiceResult> PublishAsync<T>(
            string topic, 
            T payloadDto, 
            MQTTQoSLevel mqttQoSLevel = MQTTQoSLevel.AtLeastOnce, 
            bool retain = false, 
            CancellationToken cancellationToken = default);
    }
}
