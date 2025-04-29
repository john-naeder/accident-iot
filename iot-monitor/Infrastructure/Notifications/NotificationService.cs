using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using iot_monitor.Application.Interfaces;
using iot_monitor.Application.Enum;

namespace iot_monitor.Infrastructure.Notifications
{
    public class NotificationSettings
    {
        public bool EnableEmailNotifications { get; set; } = false;
        public string SmtpServer { get; set; } = "smtp.gmail.com";
        public int SmtpPort { get; set; } = 587;
        public bool SmtpUseSsl { get; set; } = true;
        public string SmtpSenderName { get; set; } = "IoT Monitor";
        public string SmtpUsername { get; set; } = "";
        public string SmtpPassword { get; set; } = "";
        public string SenderEmail { get; set; } = "";
        public List<string> RecipientEmails { get; set; } = new List<string>();
        
        public bool EnableWebhookNotifications { get; set; } = false;
        public string WebhookUrl { get; set; } = "";
        public Dictionary<string, string> WebhookHeaders { get; set; } = new Dictionary<string, string>();
    }

    public class NotificationService : INotificationService
    {
        private readonly ILogger<NotificationService> _logger;
        private readonly NotificationSettings _settings;
        private readonly HttpClient _httpClient;

        public NotificationService(
            ILogger<NotificationService> logger,
            IOptions<NotificationSettings> settings,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _settings = settings.Value;
            _httpClient = httpClientFactory.CreateClient("NotificationClient");
        }

        public async Task SendNotificationAsync(string subject, string message, NotificationPriority priority, Dictionary<string, string> properties = null!)
        {
            try
            {
                _logger.LogInformation("Sending notification: {Subject} with priority {Priority}", subject, priority);
                
                var tasks = new List<Task>();
                
                if (_settings.EnableEmailNotifications && _settings.RecipientEmails.Count > 0)
                {
                    tasks.Add(SendEmailNotificationAsync(subject, message, priority, properties));
                }
                
                if (_settings.EnableWebhookNotifications && !string.IsNullOrEmpty(_settings.WebhookUrl))
                {
                    tasks.Add(SendWebhookNotificationAsync(subject, message, priority, properties));
                }
                
                LogNotification(subject, message, priority, properties);
                
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notification: {Subject}", subject);
            }
        }
        
        private async Task SendEmailNotificationAsync(string subject, string message, NotificationPriority priority, Dictionary<string, string> properties)
        {
            try
            {
                using var client = new SmtpClient(_settings.SmtpServer, _settings.SmtpPort)
                {
                    EnableSsl = _settings.SmtpUseSsl,
                    Credentials = new System.Net.NetworkCredential(_settings.SmtpUsername, _settings.SmtpPassword)
                };
                
                var mail = new MailMessage
                {
                    From = new MailAddress(_settings.SenderEmail, _settings.SmtpSenderName),
                    Subject = $"[{priority}] {subject}",
                    Body = FormatEmailBody(message, priority, properties),
                    IsBodyHtml = true
                };
                
                foreach (var recipient in _settings.RecipientEmails)
                {
                    mail.To.Add(recipient);
                }
                
                await client.SendMailAsync(mail);
                _logger.LogInformation("Email notification sent to {RecipientCount} recipients", _settings.RecipientEmails.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email notification");
            }
        }
        
        private async Task SendWebhookNotificationAsync(string subject, string message, NotificationPriority priority, Dictionary<string, string> properties)
        {
            try
            {
                var payload = new
                {
                    subject,
                    message,
                    priority = priority.ToString(),
                    timestamp = DateTime.UtcNow,
                    properties
                };
                
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var request = new HttpRequestMessage(HttpMethod.Post, _settings.WebhookUrl)
                {
                    Content = content
                };
                
                foreach (var header in _settings.WebhookHeaders)
                {
                    request.Headers.Add(header.Key, header.Value);
                }
                
                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Webhook notification returned non-success status code: {StatusCode}", response.StatusCode);
                }
                else
                {
                    _logger.LogInformation("Webhook notification sent successfully");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send webhook notification");
            }
        }
        
        private void LogNotification(string subject, string message, NotificationPriority priority, Dictionary<string, string> properties)
        {
            var logLevel = priority switch
            {
                NotificationPriority.Low => LogLevel.Information,
                NotificationPriority.Normal => LogLevel.Information,
                NotificationPriority.High => LogLevel.Warning,
                NotificationPriority.Critical => LogLevel.Error,
                _ => LogLevel.Information
            };
            
            _logger.Log(logLevel, "NOTIFICATION: {Subject} - {Message} [{Priority}]", subject, message, priority);
            
            if (properties != null && properties.Count > 0)
            {
                foreach (var property in properties)
                {
                    _logger.Log(logLevel, "  {Key}: {Value}", property.Key, property.Value);
                }
            }
        }
        
        private string FormatEmailBody(string message, NotificationPriority priority, Dictionary<string, string> properties)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html>");
            sb.AppendLine("<head>");
            sb.AppendLine("  <style>");
            sb.AppendLine("    body { font-family: Arial, sans-serif; }");
            sb.AppendLine("    .container { padding: 20px; }");
            sb.AppendLine("    .message { margin: 20px 0; padding: 15px; border-radius: 5px; }");
            sb.AppendLine("    .properties { margin-top: 20px; }");
            sb.AppendLine("    .property { margin: 5px 0; }");
            sb.AppendLine("    .timestamp { color: #888; font-size: 0.8em; }");
            
            string bgColor = priority switch
            {
                NotificationPriority.Critical => "#ffebee", // Light red
                NotificationPriority.High => "#fff8e1",     // Light amber
                NotificationPriority.Normal => "#e8f5e9",   // Light green
                NotificationPriority.Low => "#e3f2fd",      // Light blue
                _ => "#f5f5f5"                              // Light grey
            };
            
            sb.AppendLine($"    .message {{ background-color: {bgColor}; }}");
            sb.AppendLine("  </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("  <div class=\"container\">");
            sb.AppendLine($"    <h2>IoT Monitor Alert - {priority}</h2>");
            sb.AppendLine($"    <div class=\"message\">{message.Replace(Environment.NewLine, "<br>")}</div>");
            
            if (properties != null && properties.Count > 0)
            {
                sb.AppendLine("    <div class=\"properties\">");
                sb.AppendLine("      <h3>Details:</h3>");
                foreach (var property in properties)
                {
                    sb.AppendLine($"      <div class=\"property\"><strong>{property.Key}:</strong> {property.Value}</div>");
                }
                sb.AppendLine("    </div>");
            }
            
            sb.AppendLine($"    <div class=\"timestamp\">Time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</div>");
            sb.AppendLine("  </div>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");
            
            return sb.ToString();
        }
    }
}
