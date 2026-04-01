using ITfoxtec.Identity.Saml2;
using ITfoxtec.Identity.Saml2.MvcCore;
using ITfoxtec.Identity.Saml2.Schemas;
using ITfoxtec.Identity.Saml2.Schemas.Metadata;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OidcServer.Data;
using OidcServer.Models;
using System.Security.Claims;

namespace OidcServer.Controllers;

[Route("saml")]
public class SamlController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<SamlController> _logger;

    public SamlController(
        ApplicationDbContext db,
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        ILogger<SamlController> logger)
    {
        _db = db;
        _signInManager = signInManager;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet("{providerName}/login")]
    public async Task<IActionResult> Login(string providerName, string? returnUrl = null)
    {
        var provider = await _db.ExternalProviders
            .FirstOrDefaultAsync(p => p.Name == providerName && p.Type == "saml" && p.Enabled);

        if (provider == null)
            return NotFound("SAML provider not found.");

        try
        {
            var config = new Saml2Configuration
            {
                Issuer = $"{Request.Scheme}://{Request.Host}/saml/{providerName}",
                SingleSignOnDestination = new Uri(provider.MetadataUrl ?? throw new InvalidOperationException("MetadataUrl is required")),
            };
            config.AllowedAudienceUris.Add(config.Issuer);

            var saml2AuthnRequest = new Saml2AuthnRequest(config);
            var safeReturnUrl = (returnUrl != null && Url.IsLocalUrl(returnUrl)) ? returnUrl : "/";
            HttpContext.Session.SetString($"saml_{providerName}_returnUrl", safeReturnUrl);
            HttpContext.Session.SetString($"saml_{providerName}_relayState", saml2AuthnRequest.IdAsString);

            var redirectBinding = new Saml2RedirectBinding();
            redirectBinding.Bind(saml2AuthnRequest);
            return redirectBinding.ToActionResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating SAML login for provider {Provider}", providerName);
            return StatusCode(500, "Error initiating SAML login.");
        }
    }

    [HttpPost("{providerName}/acs")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> AssertionConsumerService(string providerName)
    {
        var provider = await _db.ExternalProviders
            .FirstOrDefaultAsync(p => p.Name == providerName && p.Type == "saml" && p.Enabled);

        if (provider == null)
            return NotFound("SAML provider not found.");

        try
        {
            var config = new Saml2Configuration
            {
                Issuer = $"{Request.Scheme}://{Request.Host}/saml/{providerName}",
            };
            config.AllowedAudienceUris.Add(config.Issuer);

            var saml2AuthnResponse = new Saml2AuthnResponse(config);
            new Saml2PostBinding().ReadSamlResponse(Request.ToGenericHttpRequest(), saml2AuthnResponse);

            var claimsIdentity = saml2AuthnResponse.ClaimsIdentity;
            var email = claimsIdentity?.FindFirst(ClaimTypes.Email)?.Value
                ?? claimsIdentity?.FindFirst("email")?.Value
                ?? claimsIdentity?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(email))
                return BadRequest("Could not retrieve email from SAML response.");

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailVerified = true,
                    DisplayName = claimsIdentity?.FindFirst(ClaimTypes.Name)?.Value,
                };
                await _userManager.CreateAsync(user);
            }

            var loginInfo = new UserLoginInfo(providerName, claimsIdentity?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? email, provider.DisplayName);
            if (await _userManager.FindByLoginAsync(loginInfo.LoginProvider, loginInfo.ProviderKey) == null)
                await _userManager.AddLoginAsync(user, loginInfo);

            await _signInManager.SignInAsync(user, isPersistent: false);

            var rawReturnUrl = HttpContext.Session.GetString($"saml_{providerName}_returnUrl");
            var returnUrl = (!string.IsNullOrEmpty(rawReturnUrl) && Url.IsLocalUrl(rawReturnUrl)) ? rawReturnUrl : "/";
            return LocalRedirect(returnUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing SAML response for provider {Provider}", providerName);
            return StatusCode(500, "Error processing SAML response.");
        }
    }

    [HttpGet("{providerName}/metadata")]
    public IActionResult Metadata(string providerName)
    {
        var config = new Saml2Configuration
        {
            Issuer = $"{Request.Scheme}://{Request.Host}/saml/{providerName}",
        };

        var entityDescriptor = new EntityDescriptor(config);
        entityDescriptor.SPSsoDescriptor = new SPSsoDescriptor
        {
            AssertionConsumerServices = new[]
            {
                new AssertionConsumerService
                {
                    Binding = ProtocolBindings.HttpPost,
                    Location = new Uri($"{Request.Scheme}://{Request.Host}/saml/{providerName}/acs"),
                }
            },
            NameIDFormats = new Uri[] { NameIdentifierFormats.Email },
        };

        return new Saml2Metadata(entityDescriptor).CreateMetadata().ToActionResult();
    }
}
