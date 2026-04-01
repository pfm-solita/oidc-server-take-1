using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using OidcServer.Models;
using System.Collections.Immutable;
using System.Security.Claims;

namespace OidcServer.Controllers;

public class AuthorizationController : Controller
{
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly IOpenIddictScopeManager _scopeManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;

    public AuthorizationController(
        IOpenIddictApplicationManager applicationManager,
        IOpenIddictScopeManager scopeManager,
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager)
    {
        _applicationManager = applicationManager;
        _scopeManager = scopeManager;
        _signInManager = signInManager;
        _userManager = userManager;
    }

    [HttpGet("/connect/authorize")]
    [HttpPost("/connect/authorize")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Authorize()
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        var result = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);

        if (!result.Succeeded)
        {
            return Challenge(
                authenticationSchemes: IdentityConstants.ApplicationScheme,
                properties: new AuthenticationProperties
                {
                    RedirectUri = Request.PathBase + Request.Path + QueryString.Create(
                        Request.HasFormContentType ? Request.Form.ToList() : Request.Query.ToList())
                });
        }

        var user = await _userManager.GetUserAsync(result.Principal);
        if (user == null)
            return Challenge(authenticationSchemes: IdentityConstants.ApplicationScheme);

        var identity = new ClaimsIdentity(
            authenticationType: "Bearer",
            nameType: OpenIddictConstants.Claims.Name,
            roleType: OpenIddictConstants.Claims.Role);

        identity.SetClaim(OpenIddictConstants.Claims.Subject, await _userManager.GetUserIdAsync(user))
                .SetClaim(OpenIddictConstants.Claims.Email, await _userManager.GetEmailAsync(user))
                .SetClaim(OpenIddictConstants.Claims.Name, user.DisplayName ?? user.Email)
                .SetClaims(OpenIddictConstants.Claims.Role, (await _userManager.GetRolesAsync(user)).ToImmutableArray());

        identity.SetScopes(request.GetScopes());

        var resources = new List<string>();
        await foreach (var resource in _scopeManager.ListResourcesAsync(identity.GetScopes()))
            resources.Add(resource);
        identity.SetResources(resources);

        identity.SetDestinations(GetDestinations);

        return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpPost("/connect/token")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Exchange()
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        if (request.IsClientCredentialsGrantType())
        {
            var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            var application = await _applicationManager.FindByClientIdAsync(request.ClientId!)
                ?? throw new InvalidOperationException("The application cannot be found.");

            identity.SetClaim(OpenIddictConstants.Claims.Subject, await _applicationManager.GetClientIdAsync(application));
            identity.SetClaim(OpenIddictConstants.Claims.Name, await _applicationManager.GetDisplayNameAsync(application));
            identity.SetScopes(request.GetScopes());

            var resources = new List<string>();
            await foreach (var resource in _scopeManager.ListResourcesAsync(identity.GetScopes()))
                resources.Add(resource);
            identity.SetResources(resources);

            identity.SetDestinations(GetDestinations);

            return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
        {
            var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            var userId = result.Principal?.GetClaim(OpenIddictConstants.Claims.Subject);

            if (string.IsNullOrEmpty(userId))
                return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

            var identity = new ClaimsIdentity(
                result.Principal!.Claims,
                authenticationType: "Bearer",
                nameType: OpenIddictConstants.Claims.Name,
                roleType: OpenIddictConstants.Claims.Role);

            identity.SetClaim(OpenIddictConstants.Claims.Subject, await _userManager.GetUserIdAsync(user))
                    .SetClaim(OpenIddictConstants.Claims.Email, await _userManager.GetEmailAsync(user))
                    .SetClaim(OpenIddictConstants.Claims.Name, user.DisplayName ?? user.Email)
                    .SetClaims(OpenIddictConstants.Claims.Role, (await _userManager.GetRolesAsync(user)).ToImmutableArray());

            identity.SetDestinations(GetDestinations);

            return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        throw new InvalidOperationException("The specified grant type is not supported.");
    }

    [HttpGet("/connect/userinfo")]
    [HttpPost("/connect/userinfo")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Userinfo()
    {
        var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var userId = result.Principal?.GetClaim(OpenIddictConstants.Claims.Subject);

        if (string.IsNullOrEmpty(userId))
            return Challenge(authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return Challenge(authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        var claims = new Dictionary<string, object>
        {
            [OpenIddictConstants.Claims.Subject] = userId,
            [OpenIddictConstants.Claims.Email] = user.Email ?? string.Empty,
            [OpenIddictConstants.Claims.EmailVerified] = user.EmailVerified,
            [OpenIddictConstants.Claims.Name] = user.DisplayName ?? user.Email ?? string.Empty,
        };

        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Any())
            claims[OpenIddictConstants.Claims.Role] = roles;

        return Ok(claims);
    }

    private static IEnumerable<string> GetDestinations(Claim claim)
    {
        switch (claim.Type)
        {
            case OpenIddictConstants.Claims.Name:
                yield return OpenIddictConstants.Destinations.AccessToken;
                if (claim.Subject != null && claim.Subject.HasScope(OpenIddictConstants.Permissions.Scopes.Profile))
                    yield return OpenIddictConstants.Destinations.IdentityToken;
                yield break;

            case OpenIddictConstants.Claims.Email:
                yield return OpenIddictConstants.Destinations.AccessToken;
                if (claim.Subject != null && claim.Subject.HasScope(OpenIddictConstants.Permissions.Scopes.Email))
                    yield return OpenIddictConstants.Destinations.IdentityToken;
                yield break;

            case OpenIddictConstants.Claims.Role:
                yield return OpenIddictConstants.Destinations.AccessToken;
                if (claim.Subject != null && claim.Subject.HasScope(OpenIddictConstants.Permissions.Scopes.Roles))
                    yield return OpenIddictConstants.Destinations.IdentityToken;
                yield break;

            default:
                yield return OpenIddictConstants.Destinations.AccessToken;
                yield break;
        }
    }
}
