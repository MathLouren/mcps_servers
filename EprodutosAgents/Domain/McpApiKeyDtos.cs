using System.Text.Json.Serialization;

namespace EprodutosAgents.Domain;

public sealed record CreateMcpApiKeyRequest(
    [property: JsonPropertyName("user_id")] string? UserId,
    [property: JsonPropertyName("email")] string? Email,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("created_by")] string? CreatedBy,
    [property: JsonPropertyName("expires_at")] DateTime? ExpiresAt);

public sealed record McpApiKeyCreatedResponse(
    [property: JsonPropertyName("api_key")] string ApiKey,
    [property: JsonPropertyName("key_id")] string KeyId,
    [property: JsonPropertyName("user_id")] string UserId,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("created_at")] DateTime CreatedAt,
    [property: JsonPropertyName("expires_at")] DateTime? ExpiresAt,
    [property: JsonPropertyName("message")] string Message);

public sealed record McpApiKeyMetadataResponse(
    [property: JsonPropertyName("key_id")] string KeyId,
    [property: JsonPropertyName("user_id")] string UserId,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("created_at")] DateTime CreatedAt,
    [property: JsonPropertyName("expires_at")] DateTime? ExpiresAt,
    [property: JsonPropertyName("last_used_at")] DateTime? LastUsedAt,
    [property: JsonPropertyName("revoked_at")] DateTime? RevokedAt,
    [property: JsonPropertyName("revoked_reason")] string? RevokedReason);
