using MailKit.Net.Smtp;
using MimeKit;

namespace OidcServer.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendEmailAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        var emailSettings = _config.GetSection("Email");
        var fromAddress = emailSettings["From"] ?? "noreply@oidcserver.local";
        var smtpHost = emailSettings["SmtpHost"] ?? "localhost";
        var smtpPort = int.Parse(emailSettings["SmtpPort"] ?? "25");
        var smtpUser = emailSettings["SmtpUser"];
        var smtpPass = emailSettings["SmtpPassword"];
        var useSsl = bool.Parse(emailSettings["UseSsl"] ?? "false");

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(fromAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlBody };

        try
        {
            using var client = new SmtpClient();
            await client.ConnectAsync(smtpHost, smtpPort, useSsl ? MailKit.Security.SecureSocketOptions.SslOnConnect : MailKit.Security.SecureSocketOptions.None, cancellationToken);
            if (!string.IsNullOrEmpty(smtpUser))
                await client.AuthenticateAsync(smtpUser, smtpPass, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}", to);
        }
    }

    public async Task SendVerificationEmailAsync(string to, string verificationLink, CancellationToken cancellationToken = default)
    {
        var encodedLink = System.Net.WebUtility.HtmlEncode(verificationLink);
        var body = $"<p>Please verify your email address by clicking <a href='{encodedLink}'>here</a>.</p><p>Or copy this link: {encodedLink}</p>";
        await SendEmailAsync(to, "Verify your email address", body, cancellationToken);
    }

    public async Task SendTwoFactorCodeAsync(string to, string code, CancellationToken cancellationToken = default)
    {
        var body = $"<p>Your two-factor authentication code is: <strong>{code}</strong></p><p>This code expires in 10 minutes.</p>";
        await SendEmailAsync(to, "Your authentication code", body, cancellationToken);
    }
}
