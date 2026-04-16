using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EprodutosAgents.Domain;

[BsonIgnoreExtraElements]
public sealed class ProductDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    [JsonPropertyName("_id")]
    public string? Id { get; set; }

    [BsonElement("pn")]
    [JsonPropertyName("pn")]
    public string Pn { get; set; } = string.Empty;

    [BsonElement("category")]
    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [BsonElement("manufacturer")]
    [JsonPropertyName("manufacturer")]
    public string? Manufacturer { get; set; }
}
