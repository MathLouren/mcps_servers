using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EprodutosAgents.Domain;

[BsonIgnoreExtraElements]
public sealed class McpAuditLogDocument
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
    public string Role { get; set; } = string.Empty;

    [BsonElement("apiKeyId")]
    [JsonPropertyName("apiKeyId")]
    public string ApiKeyId { get; set; } = string.Empty;

    [BsonElement("toolName")]
    [JsonPropertyName("toolName")]
    public string ToolName { get; set; } = string.Empty;

    [BsonElement("arguments")]
    [JsonPropertyName("arguments")]
    public BsonDocument Arguments { get; set; } = [];

    [BsonElement("status")]
    [JsonPropertyName("status")]
    public string Status { get; set; } = "success";

    [BsonElement("durationMs")]
    [JsonPropertyName("durationMs")]
    public long DurationMs { get; set; }

    [BsonElement("resultCount")]
    [JsonPropertyName("resultCount")]
    public long? ResultCount { get; set; }

    [BsonElement("ipAddress")]
    [JsonPropertyName("ipAddress")]
    public string? IpAddress { get; set; }

    [BsonElement("userAgent")]
    [JsonPropertyName("userAgent")]
    public string? UserAgent { get; set; }

    [BsonElement("errorMessage")]
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    [BsonElement("createdAt")]
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
