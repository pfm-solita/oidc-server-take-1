namespace OidcServer.Services;

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default);
    Task SendVerificationEmailAsync(string to, string verificationLink, CancellationToken cancellationToken = default);
    Task SendTwoFactorCodeAsync(string to, string code, CancellationToken cancellationToken = default);
}
