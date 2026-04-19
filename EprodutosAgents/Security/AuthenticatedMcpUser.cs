namespace EprodutosAgents.Security;

public sealed record AuthenticatedMcpUser(
    string UserId,
    string Email,
    string Name,
    string Role,
    string ApiKeyId,
    IReadOnlyList<string> AllowedProcesses,
    IReadOnlyList<string> AllowedManufacturers)
{
    public bool CanSeeCostPrice => EprodutosAgents.Domain.McpRoles.CanSeeCostPrice(Role);
}

public static class McpClaimTypes
{
    public const string ApiKeyId = "eprodutos:mcp_api_key_id";
    public const string AllowedProcess = "eprodutos:allowed_process";
    public const string AllowedManufacturer = "eprodutos:allowed_manufacturer";
}
