using Microsoft.AspNetCore.Identity;

namespace OidcServer.Models;

public class ApplicationUser : IdentityUser
{
    public bool EmailVerified { get; set; }
    public string? TwoFactorCode { get; set; }
    public DateTime? TwoFactorCodeExpiry { get; set; }
    public string? DisplayName { get; set; }
}
