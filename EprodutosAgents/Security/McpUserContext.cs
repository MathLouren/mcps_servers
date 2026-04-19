using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace EprodutosAgents.Security;

public sealed class McpUserContext(IHttpContextAccessor httpContextAccessor)
{
    public AuthenticatedMcpUser GetRequiredUser()
    {
        var principal = httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            throw new UnauthorizedAccessException("API key MCP ausente ou invalida.");
        }

        var userId = FindClaim(principal, ClaimTypes.NameIdentifier);
        var email = FindClaim(principal, ClaimTypes.Email);
        var name = FindClaim(principal, ClaimTypes.Name) ?? email;
        var role = FindClaim(principal, ClaimTypes.Role) ?? EprodutosAgents.Domain.McpRoles.Customer;
        var apiKeyId = FindClaim(principal, McpClaimTypes.ApiKeyId);

        if (string.IsNullOrWhiteSpace(userId) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(apiKeyId))
        {
            throw new UnauthorizedAccessException("Identidade MCP incompleta.");
        }

        return new AuthenticatedMcpUser(
            userId,
            email,
            name ?? email,
            role,
            apiKeyId,
            principal.FindAll(McpClaimTypes.AllowedProcess).Select(static claim => claim.Value).ToArray(),
            principal.FindAll(McpClaimTypes.AllowedManufacturer).Select(static claim => claim.Value).ToArray());
    }

    private static string? FindClaim(ClaimsPrincipal principal, string type)
    {
        return principal.Claims.FirstOrDefault(claim => claim.Type == type)?.Value;
    }
}
