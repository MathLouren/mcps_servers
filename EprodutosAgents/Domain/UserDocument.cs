using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EprodutosAgents.Domain;

[BsonIgnoreExtraElements]
public sealed class UserDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    [JsonPropertyName("_id")]
    public string? Id { get; set; }

    [BsonElement("name")]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("email")]
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [BsonElement("role")]
    [JsonPropertyName("role")]
    public string Role { get; set; } = McpRoles.Customer;

    [BsonElement("allowedProcesses")]
    [JsonPropertyName("allowedProcesses")]
    public List<string>? AllowedProcesses { get; set; }

    [BsonElement("allowedManufacturers")]
    [JsonPropertyName("allowedManufacturers")]
    public List<string>? AllowedManufacturers { get; set; }

    [BsonElement("acceptedTerms")]
    [JsonPropertyName("acceptedTerms")]
    public bool AcceptedTerms { get; set; }
}

public static class McpRoles
{
    public const string Admin = "admin";
    public const string Manager = "manager";
    public const string Employee = "employee";
    public const string Customer = "customer";
    public const string Distributor = "distributor";
    public const string Manufacturer = "manufacturer";

    public static bool CanSeeCostPrice(string? role)
    {
        return Is(role, Admin) || Is(role, Manager) || Is(role, Employee);
    }

    private static bool Is(string? role, string expected)
    {
        return string.Equals(role, expected, StringComparison.OrdinalIgnoreCase);
    }
}
