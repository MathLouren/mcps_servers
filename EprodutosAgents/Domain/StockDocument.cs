using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EprodutosAgents.Domain;

[BsonIgnoreExtraElements]
public sealed class StockDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    [JsonPropertyName("_id")]
    public string? Id { get; set; }

    [BsonElement("pn")]
    [JsonPropertyName("pn")]
    public string Pn { get; set; } = string.Empty;

    [BsonElement("uf")]
    [JsonPropertyName("uf")]
    public string? Uf { get; set; }

    [BsonElement("cd")]
    [JsonPropertyName("cd")]
    public string? Cd { get; set; }

    [BsonElement("description")]
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [BsonElement("process")]
    [JsonPropertyName("process")]
    public string? Process { get; set; }

    [BsonElement("quantity")]
    [JsonPropertyName("quantity")]
    public int? Quantity { get; set; }

    [BsonElement("price")]
    [JsonPropertyName("price")]
    public long? Price { get; set; }

    [BsonElement("sale_price")]
    [JsonPropertyName("sale_price")]
    public long? SalePrice { get; set; }

    [BsonElement("currency")]
    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    [BsonElement("date")]
    [JsonPropertyName("date")]
    public DateTime? Date { get; set; }

    [BsonElement("status")]
    [JsonPropertyName("status")]
    public int? Status { get; set; }

    [BsonElement("validity")]
    [JsonPropertyName("validity")]
    public string? Validity { get; set; }
}
