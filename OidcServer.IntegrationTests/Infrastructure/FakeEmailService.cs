using System.Collections.Concurrent;
using OidcServer.Services;

namespace OidcServer.IntegrationTests.Infrastructure;

/// <summary>
/// In-memory email service that captures sent messages for test assertions.
/// </summary>
public class FakeEmailService : IEmailService
{
    private readonly ConcurrentDictionary<string, string> _twoFactorCodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _verificationLinks = new(StringComparer.OrdinalIgnoreCase);

    public string? GetTwoFactorCode(string email) =>
        _twoFactorCodes.TryGetValue(email, out var code) ? code : null;

    public string? GetVerificationLink(string email) =>
        _verificationLinks.TryGetValue(email, out var link) ? link : null;

    public Task SendEmailAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SendVerificationEmailAsync(string to, string verificationLink, CancellationToken cancellationToken = default)
    {
        _verificationLinks[to] = verificationLink;
        return Task.CompletedTask;
    }

    public Task SendTwoFactorCodeAsync(string to, string code, CancellationToken cancellationToken = default)
    {
        _twoFactorCodes[to] = code;
        return Task.CompletedTask;
    }

    public void Clear()
    {
        _twoFactorCodes.Clear();
        _verificationLinks.Clear();
    }
}
