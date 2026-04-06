// ============================================================
// KITSUNE – Notification Service
// Sends webhook (Teams/Slack/generic) and SMTP email alerts
// Triggered by: Apply, Rollback, ScheduledBackup failures
// ============================================================
using System;
using System.Net.Http;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Kitsune.Backend.Services
{
    public enum NotificationEvent
    {
        ApplySuccess, ApplyFailed, RollbackPerformed,
        BackupScheduledOk, BackupScheduledFailed,
        ValidationFailed, HighRiskDetected
    }

    public class NotificationPayload
    {
        public NotificationEvent Event      { get; set; }
        public string            ObjectName { get; set; } = "";
        public string            Message    { get; set; } = "";
        public string            Detail     { get; set; } = "";
        public string            User       { get; set; } = "system";
        public DateTime          Timestamp  { get; set; } = DateTime.UtcNow;
    }

    public interface INotificationService
    {
        Task SendAsync(NotificationPayload payload);
    }

    public class NotificationService : INotificationService
    {
        private readonly IConfiguration _cfg;
        private readonly HttpClient     _http;
        private readonly ILogger<NotificationService> _log;

        public NotificationService(IConfiguration cfg, ILogger<NotificationService> log)
        {
            _cfg  = cfg;
            _log  = log;
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        }

        public async Task SendAsync(NotificationPayload payload)
        {
            var tasks = new System.Collections.Generic.List<Task>();

            var webhookUrl = _cfg["Notifications:WebhookUrl"];
            if (!string.IsNullOrEmpty(webhookUrl))
                tasks.Add(SendWebhookAsync(webhookUrl, payload));

            var smtpEnabled = _cfg.GetValue<bool>("Notifications:Smtp:Enabled");
            if (smtpEnabled)
                tasks.Add(SendEmailAsync(payload));

            await Task.WhenAll(tasks);
        }

        // ── Generic webhook (Teams / Slack / custom) ──────────
        private async Task SendWebhookAsync(string url, NotificationPayload payload)
        {
            try
            {
                var icon    = payload.Event is NotificationEvent.ApplyFailed
                              or NotificationEvent.BackupScheduledFailed
                              or NotificationEvent.ValidationFailed ? "🔴" : "🟢";
                var body    = new
                {
                    text = $"{icon} **KITSUNE** | {payload.Event}\n" +
                           $"Object: `{payload.ObjectName}`\n" +
                           $"{payload.Message}\n" +
                           $"_{payload.Timestamp:u}_"
                };
                var json    = JsonSerializer.Serialize(body);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var resp    = await _http.PostAsync(url, content);
                resp.EnsureSuccessStatusCode();
                _log.LogDebug("Webhook sent for {Event}", payload.Event);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Webhook delivery failed for {Event}", payload.Event);
            }
        }

        // ── SMTP email ────────────────────────────────────────
        private async Task SendEmailAsync(NotificationPayload payload)
        {
            try
            {
                var host    = _cfg["Notifications:Smtp:Host"]    ?? "localhost";
                var port    = _cfg.GetValue<int>("Notifications:Smtp:Port", 587);
                var user    = _cfg["Notifications:Smtp:Username"] ?? "";
                var pass    = _cfg["Notifications:Smtp:Password"] ?? "";
                var from    = _cfg["Notifications:Smtp:From"]    ?? "kitsune@localhost";
                var to      = _cfg["Notifications:Smtp:To"]      ?? "";

                if (string.IsNullOrEmpty(to)) return;

                using var smtp = new SmtpClient(host, port)
                {
                    EnableSsl         = _cfg.GetValue<bool>("Notifications:Smtp:UseSsl", true),
                    Credentials       = new System.Net.NetworkCredential(user, pass),
                    DeliveryMethod    = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                };

                var mail = new MailMessage(from, to)
                {
                    Subject    = $"[KITSUNE] {payload.Event} – {payload.ObjectName}",
                    Body       = $"Event: {payload.Event}\nObject: {payload.ObjectName}\n\n{payload.Message}\n\n{payload.Detail}\n\nTimestamp: {payload.Timestamp:u}",
                    IsBodyHtml = false,
                };

                await smtp.SendMailAsync(mail);
                _log.LogDebug("Email sent to {To} for {Event}", to, payload.Event);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Email delivery failed for {Event}", payload.Event);
            }
        }
    }
}
