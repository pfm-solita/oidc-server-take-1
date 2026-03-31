using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OidcServer.Data;
using OidcServer.Models;
using OidcServer.Services;

namespace OidcServer.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IEmailService _emailService;
    private readonly ILogger<AccountController> _logger;
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _configuration;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IEmailService emailService,
        ILogger<AccountController> logger,
        ApplicationDbContext db,
        IConfiguration configuration)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _emailService = emailService;
        _logger = logger;
        _db = db;
        _configuration = configuration;
    }

    [HttpGet("/account/login")]
    public async Task<IActionResult> Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        await PopulateExternalProvidersAsync();
        return View();
    }

    [HttpPost("/account/login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string email, string password, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            await PopulateExternalProvidersAsync();
            return View();
        }

        if (!user.EmailVerified)
        {
            ModelState.AddModelError(string.Empty, "Please verify your email before logging in.");
            await PopulateExternalProvidersAsync();
            return View();
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            await PopulateExternalProvidersAsync();
            return View();
        }

        if (!_configuration.GetValue<bool>("Auth:TwoFactorEnabled", false))
        {
            await _signInManager.SignInAsync(user, isPersistent: false);
            var safeUrl = (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) ? returnUrl : "/";
            return LocalRedirect(safeUrl);
        }

        var code = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        user.TwoFactorCode = code;
        user.TwoFactorCodeExpiry = DateTime.UtcNow.AddMinutes(10);
        await _userManager.UpdateAsync(user);

        await _emailService.SendTwoFactorCodeAsync(user.Email!, code);

        HttpContext.Session.SetString("2fa_user_id", user.Id);
        HttpContext.Session.SetString("2fa_return_url", returnUrl ?? "/");

        return RedirectToAction(nameof(TwoFactor));
    }

    [HttpGet("/account/two-factor")]
    public IActionResult TwoFactor()
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("2fa_user_id")))
            return RedirectToAction(nameof(Login));
        return View();
    }

    [HttpPost("/account/two-factor")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TwoFactor(string code)
    {
        var userId = HttpContext.Session.GetString("2fa_user_id");
        if (string.IsNullOrEmpty(userId))
            return RedirectToAction(nameof(Login));

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return RedirectToAction(nameof(Login));

        if (user.TwoFactorCode != code || user.TwoFactorCodeExpiry < DateTime.UtcNow)
        {
            ModelState.AddModelError(string.Empty, "Invalid or expired code.");
            return View();
        }

        user.TwoFactorCode = null;
        user.TwoFactorCodeExpiry = null;
        await _userManager.UpdateAsync(user);

        HttpContext.Session.Remove("2fa_user_id");
        var rawReturnUrl = HttpContext.Session.GetString("2fa_return_url");
        var returnUrl = (!string.IsNullOrEmpty(rawReturnUrl) && Url.IsLocalUrl(rawReturnUrl)) ? rawReturnUrl : "/";
        HttpContext.Session.Remove("2fa_return_url");

        await _signInManager.SignInAsync(user, isPersistent: false);

        return LocalRedirect(returnUrl);
    }

    [HttpGet("/account/register")]
    public IActionResult Register()
    {
        return View();
    }

    [HttpPost("/account/register")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(string email, string password, string confirmPassword, string? displayName)
    {
        if (password != confirmPassword)
        {
            ModelState.AddModelError(string.Empty, "Passwords do not match.");
            return View();
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = displayName,
            EmailVerified = false
        };

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View();
        }

        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var verificationLink = Url.Action(nameof(VerifyEmail), "Account",
            new { userId = user.Id, token }, Request.Scheme)!;

        await _emailService.SendVerificationEmailAsync(user.Email!, verificationLink);

        ViewData["Message"] = "Registration successful! Please check your email to verify your account.";
        return View("RegisterConfirmation");
    }

    [HttpGet("/account/verify-email")]
    public async Task<IActionResult> VerifyEmail(string userId, string token)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return NotFound();

        var result = await _userManager.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
        {
            ViewData["Error"] = "Email verification failed.";
            return View("VerifyEmailResult");
        }

        user.EmailVerified = true;
        await _userManager.UpdateAsync(user);

        ViewData["Success"] = true;
        return View("VerifyEmailResult");
    }

    [HttpGet("/account/logout")]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        HttpContext.Session.Clear();
        return RedirectToAction(nameof(Login));
    }

    [HttpGet("/account/external-login")]
    public IActionResult ExternalLogin(string provider, string? returnUrl = null)
    {
        var redirectUrl = Url.Action("ExternalLoginCallback", "Account", new { returnUrl });
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return Challenge(properties, provider);
    }

    [HttpGet("/account/external-login-callback")]
    public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null)
    {
        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null)
            return RedirectToAction(nameof(Login));

        var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false);
        if (result.Succeeded)
        {
            var safeUrl = (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) ? returnUrl : "/";
            return LocalRedirect(safeUrl);
        }

        var email = info.Principal.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(email))
        {
            ViewData["Error"] = "Could not retrieve email from external provider.";
            await PopulateExternalProvidersAsync();
            return View("Login");
        }

        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailVerified = true,
                DisplayName = info.Principal.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
            };
            await _userManager.CreateAsync(user);
        }

        await _userManager.AddLoginAsync(user, info);
        await _signInManager.SignInAsync(user, isPersistent: false);
        var safeReturnUrl = (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) ? returnUrl : "/";
        return LocalRedirect(safeReturnUrl);
    }

    private async Task PopulateExternalProvidersAsync()
    {
        ViewData["ExternalProviders"] = await _db.ExternalProviders
            .Where(p => p.Enabled)
            .OrderBy(p => p.DisplayName)
            .ToListAsync();
    }
}
