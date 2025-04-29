using iot_monitor.Application.Enum;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace iot_monitor.Application.Interfaces
{
    public interface INotificationService
    {
        /// <summary>
        /// Sends a notification with the specified subject, message, priority, and optional properties
        /// </summary>
        /// <param name="subject">Notification subject line</param>
        /// <param name="message">Notification message body</param>
        /// <param name="priority">Priority level of the notification</param>
        /// <param name="properties">Optional additional properties to include with the notification</param>
        /// <returns>Task representing the asynchronous operation</returns>
        Task SendNotificationAsync(string subject, string message, NotificationPriority priority, Dictionary<string, string> properties = null!);
    }
}
