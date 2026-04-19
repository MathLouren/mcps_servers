using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EprodutosAgents.Domain;

[BsonIgnoreExtraElements]
public sealed class McpApiKeyDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    [JsonPropertyName("_id")]
    public string? Id { get; set; }

    [BsonElement("userId")]
    [BsonRepresentation(BsonType.ObjectId)]
    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("userEmail")]
    [JsonPropertyName("userEmail")]
    public string UserEmail { get; set; } = string.Empty;

    [BsonElement("userName")]
    [JsonPropertyName("userName")]
    public string UserName { get; set; } = string.Empty;

    [BsonElement("role")]
    [JsonPropertyName("role")]
    public string Role { get; set; } = McpRoles.Customer;

    [BsonElement("keyId")]
    [JsonPropertyName("keyId")]
    public string KeyId { get; set; } = string.Empty;

    [BsonElement("secretHash")]
    [JsonPropertyName("secretHash")]
    public string SecretHash { get; set; } = string.Empty;

    [BsonElement("secretSalt")]
    [JsonPropertyName("secretSalt")]
    public string SecretSalt { get; set; } = string.Empty;

    [BsonElement("hashIterations")]
    [JsonPropertyName("hashIterations")]
    public int HashIterations { get; set; }

    [BsonElement("name")]
    [JsonPropertyName("name")]
    public string Name { get; set; } = "MCP";

    [BsonElement("createdAt")]
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("expiresAt")]
    [JsonPropertyName("expiresAt")]
    public DateTime? ExpiresAt { get; set; }

    [BsonElement("lastUsedAt")]
    [JsonPropertyName("lastUsedAt")]
    public DateTime? LastUsedAt { get; set; }

    [BsonElement("revokedAt")]
    [JsonPropertyName("revokedAt")]
    public DateTime? RevokedAt { get; set; }

    [BsonElement("revokedReason")]
    [JsonPropertyName("revokedReason")]
    public string? RevokedReason { get; set; }

    [BsonElement("createdBy")]
    [JsonPropertyName("createdBy")]
    public string? CreatedBy { get; set; }
}
