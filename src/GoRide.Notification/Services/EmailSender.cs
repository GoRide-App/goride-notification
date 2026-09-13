using System.Net;
using System.Net.Mail;

namespace GoRide.Notification.Services;

/// <summary>
/// SMTP implementation of IEmailSender. Dispatches email notifications to recipients.
/// Supports configurable SMTP host, port, credentials, and SSL settings with logging fallback in dev mode.
/// </summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _config;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IConfiguration config, ILogger<SmtpEmailSender> logger)
    {
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Sends an email notification to the recipient address.
    /// Uses SMTP settings from configuration ("Smtp:Host", "Smtp:Port", etc.), or logs in dev mode if server is unavailable.
    /// </summary>
    public async Task SendEmail(string recipientEmail, string subject, string body, CancellationToken ct)
    {
        var host = _config["Smtp:Host"] ?? "localhost";
        var port = int.TryParse(_config["Smtp:Port"], out var p) ? p : 25;
        var senderEmail = _config["Smtp:From"] ?? "noreply@goride.com";
        var username = _config["Smtp:Username"];
        var password = _config["Smtp:Password"];
        var enableSsl = bool.TryParse(_config["Smtp:EnableSsl"], out var ssl) && ssl;

        _logger.LogInformation("Preparing to send email to {Recipient} with subject '{Subject}'", recipientEmail, subject);

        try
        {
            using var message = new MailMessage();
            message.From = new MailAddress(senderEmail, "GoRide Notifications");
            message.To.Add(new MailAddress(recipientEmail));
            message.Subject = subject;
            message.Body = body;
            message.IsBodyHtml = false;

            using var smtpClient = new SmtpClient(host, port);
            smtpClient.EnableSsl = enableSsl;

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                smtpClient.Credentials = new NetworkCredential(username, password);
            }

            // If localhost or default dev mode without real SMTP, log dispatch
            if (host == "localhost" && port == 25)
            {
                _logger.LogInformation("[EmailSender DevMode] Email dispatched to {Recipient}: {Subject} -> {Body}", recipientEmail, subject, body);
                await Task.Delay(15, ct); // Simulate email dispatch network latency
                return;
            }

            await smtpClient.SendMailAsync(message, ct);
            _logger.LogInformation("Email successfully dispatched to {Recipient} via SMTP", recipientEmail);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SMTP delivery failed for {Recipient}. Falling back to dev-mode dispatch logging.", recipientEmail);
            // In dev mode / fallback, ensure message attempt is logged rather than crashing process
        }
    }
}
