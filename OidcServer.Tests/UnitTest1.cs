namespace OidcServer.Tests;

public class ApplicationUserTests
{
    [Fact]
    public void ApplicationUser_DefaultValues_AreCorrect()
    {
        var user = new OidcServer.Models.ApplicationUser();
        Assert.False(user.EmailVerified);
        Assert.Null(user.TwoFactorCode);
        Assert.Null(user.TwoFactorCodeExpiry);
        Assert.Null(user.DisplayName);
    }

    [Fact]
    public void ExternalProvider_DefaultValues_AreCorrect()
    {
        var provider = new OidcServer.Models.ExternalProvider();
        Assert.True(provider.Enabled);
        Assert.Equal(string.Empty, provider.Name);
        Assert.Equal(string.Empty, provider.DisplayName);
        Assert.Equal(string.Empty, provider.Type);
    }
}
