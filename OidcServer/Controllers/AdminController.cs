using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OidcServer.Data;
using OidcServer.Models;

namespace OidcServer.Controllers;

[Authorize(Roles = "Admin")]
[Route("admin")]
public class AdminController : Controller
{
    private readonly IOpenIddictApplicationManager _appManager;
    private readonly IOpenIddictScopeManager _scopeManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ApplicationDbContext _db;

    public AdminController(
        IOpenIddictApplicationManager appManager,
        IOpenIddictScopeManager scopeManager,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ApplicationDbContext db)
    {
        _appManager = appManager;
        _scopeManager = scopeManager;
        _userManager = userManager;
        _roleManager = roleManager;
        _db = db;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet("clients")]
    public async Task<IActionResult> Clients()
    {
        var clients = new List<object>();
        await foreach (var app in _appManager.ListAsync())
        {
            clients.Add(new
            {
                ClientId = await _appManager.GetClientIdAsync(app),
                DisplayName = await _appManager.GetDisplayNameAsync(app),
                Type = await _appManager.GetClientTypeAsync(app),
            });
        }
        ViewData["Clients"] = clients;
        return View();
    }

    [HttpGet("clients/create")]
    public IActionResult CreateClient()
    {
        return View();
    }

    [HttpPost("clients/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateClient(string clientId, string clientSecret, string displayName, string clientType, string? redirectUri, string? postLogoutRedirectUri, string? scopes)
    {
        if (await _appManager.FindByClientIdAsync(clientId) != null)
        {
            ModelState.AddModelError(string.Empty, "A client with this ID already exists.");
            return View();
        }

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientSecret = clientType == "confidential" ? clientSecret : null,
            DisplayName = displayName,
            ClientType = clientType == "confidential"
                ? OpenIddictConstants.ClientTypes.Confidential
                : OpenIddictConstants.ClientTypes.Public,
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Authorization,
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.Endpoints.Logout,
                OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                OpenIddictConstants.Permissions.ResponseTypes.Code,
                OpenIddictConstants.Permissions.Scopes.Email,
                OpenIddictConstants.Permissions.Scopes.Profile,
                OpenIddictConstants.Permissions.Scopes.Roles,
                OpenIddictConstants.Permissions.Prefixes.Scope + "openid",
            }
        };

        if (!string.IsNullOrEmpty(redirectUri))
            descriptor.RedirectUris.Add(new Uri(redirectUri));

        if (!string.IsNullOrEmpty(postLogoutRedirectUri))
            descriptor.PostLogoutRedirectUris.Add(new Uri(postLogoutRedirectUri));

        if (!string.IsNullOrEmpty(scopes))
        {
            foreach (var scope in scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + scope);
        }

        await _appManager.CreateAsync(descriptor);
        return RedirectToAction(nameof(Clients));
    }

    [HttpPost("clients/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteClient(string clientId)
    {
        var app = await _appManager.FindByClientIdAsync(clientId);
        if (app != null)
            await _appManager.DeleteAsync(app);
        return RedirectToAction(nameof(Clients));
    }

    [HttpGet("scopes")]
    public async Task<IActionResult> Scopes()
    {
        var scopes = new List<object>();
        await foreach (var scope in _scopeManager.ListAsync())
        {
            scopes.Add(new
            {
                Name = await _scopeManager.GetNameAsync(scope),
                DisplayName = await _scopeManager.GetDisplayNameAsync(scope),
            });
        }
        ViewData["Scopes"] = scopes;
        return View();
    }

    [HttpPost("scopes/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateScope(string name, string displayName, string? resources)
    {
        var descriptor = new OpenIddictScopeDescriptor
        {
            Name = name,
            DisplayName = displayName,
        };

        if (!string.IsNullOrEmpty(resources))
        {
            foreach (var resource in resources.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                descriptor.Resources.Add(resource);
        }

        await _scopeManager.CreateAsync(descriptor);
        return RedirectToAction(nameof(Scopes));
    }

    [HttpPost("scopes/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteScope(string name)
    {
        var scope = await _scopeManager.FindByNameAsync(name);
        if (scope != null)
            await _scopeManager.DeleteAsync(scope);
        return RedirectToAction(nameof(Scopes));
    }

    [HttpGet("external-providers")]
    public async Task<IActionResult> ExternalProviders()
    {
        var providers = await _db.ExternalProviders.ToListAsync();
        return View(providers);
    }

    [HttpPost("external-providers/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateExternalProvider(ExternalProvider provider)
    {
        _db.ExternalProviders.Add(provider);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(ExternalProviders));
    }

    [HttpPost("external-providers/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteExternalProvider(int id)
    {
        var provider = await _db.ExternalProviders.FindAsync(id);
        if (provider != null)
        {
            _db.ExternalProviders.Remove(provider);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(ExternalProviders));
    }

    [HttpGet("users")]
    public async Task<IActionResult> Users()
    {
        var users = await _userManager.Users.ToListAsync();
        return View(users);
    }

    [HttpPost("users/toggle-admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleAdmin(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return NotFound();

        if (await _userManager.IsInRoleAsync(user, "Admin"))
            await _userManager.RemoveFromRoleAsync(user, "Admin");
        else
            await _userManager.AddToRoleAsync(user, "Admin");

        return RedirectToAction(nameof(Users));
    }
}
