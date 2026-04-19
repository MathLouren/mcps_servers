using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace EprodutosAgents.Security;

public sealed class McpApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    McpApiKeyService apiKeyService)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "McpApiKey";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var apiKey = GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return AuthenticateResult.NoResult();
        }

        var user = await apiKeyService.ValidateAsync(apiKey, Context.RequestAborted);
        if (user is null)
        {
            return AuthenticateResult.Fail("API key MCP invalida.");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Role, user.Role),
            new(McpClaimTypes.ApiKeyId, user.ApiKeyId)
        };

        claims.AddRange(user.AllowedProcesses.Select(process => new Claim(McpClaimTypes.AllowedProcess, process)));
        claims.AddRange(user.AllowedManufacturers.Select(manufacturer => new Claim(McpClaimTypes.AllowedManufacturer, manufacturer)));

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }

    private string? GetApiKey()
    {
        var authorization = Request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(authorization) &&
            authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return NormalizeApiKey(authorization["Bearer ".Length..]);
        }

        var headerKey = Request.Headers["X-Api-Key"].FirstOrDefault();
        return NormalizeApiKey(headerKey);
    }

    private static string? NormalizeApiKey(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        var trimmed = apiKey.Trim();
        return trimmed.Length > McpSecurityLimits.MaxApiKeyLength ? null : trimmed;
    }
}
