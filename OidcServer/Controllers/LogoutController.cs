using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Server.AspNetCore;
using OidcServer.Models;

namespace OidcServer.Controllers;

public class LogoutController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;

    public LogoutController(SignInManager<ApplicationUser> signInManager)
    {
        _signInManager = signInManager;
    }

    [HttpGet("/connect/logout")]
    [HttpPost("/connect/logout")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Logout()
    {
        var request = HttpContext.GetOpenIddictServerRequest();

        await _signInManager.SignOutAsync();
        HttpContext.Session.Clear();

        return SignOut(
            authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            properties: new AuthenticationProperties
            {
                RedirectUri = "/"
            });
    }
}
