using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using System.Text.RegularExpressions;

namespace EprodutosAgents.Configuration;

public sealed partial record MongoOptions(
    string ConnectionString,
    string DatabaseName,
    string ProductsCollectionName,
    string StocksCollectionName,
    string UsersCollectionName,
    string McpApiKeysCollectionName,
    string McpAuditLogsCollectionName)
{
    public const string SectionName = "MongoDb";

    public static MongoOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);

        var options = new MongoOptions(
            FirstNonEmpty(
                Environment.GetEnvironmentVariable("EPRODUTOS_MONGO_CONNECTION_STRING"),
                section["ConnectionString"],
                "mongodb://localhost:27017/"),
            FirstNonEmpty(
                Environment.GetEnvironmentVariable("EPRODUTOS_MONGO_DATABASE"),
                section["DatabaseName"],
                "eprodutos"),
            FirstNonEmpty(
                Environment.GetEnvironmentVariable("EPRODUTOS_PRODUCTS_COLLECTION"),
                section["ProductsCollectionName"],
                "products"),
            FirstNonEmpty(
                Environment.GetEnvironmentVariable("EPRODUTOS_STOCKS_COLLECTION"),
                section["StocksCollectionName"],
                "stocks"),
            FirstNonEmpty(
                Environment.GetEnvironmentVariable("EPRODUTOS_USERS_COLLECTION"),
                section["UsersCollectionName"],
                "users"),
            FirstNonEmpty(
                Environment.GetEnvironmentVariable("EPRODUTOS_MCP_API_KEYS_COLLECTION"),
                section["McpApiKeysCollectionName"],
                "mcp_api_keys"),
            FirstNonEmpty(
                Environment.GetEnvironmentVariable("EPRODUTOS_MCP_AUDIT_LOGS_COLLECTION"),
                section["McpAuditLogsCollectionName"],
                "mcp_audit_logs"));

        options.Validate();
        return options;
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    private void Validate()
    {
        _ = MongoUrl.Create(ConnectionString);
        ValidateMongoName(DatabaseName, nameof(DatabaseName));
        ValidateMongoName(ProductsCollectionName, nameof(ProductsCollectionName));
        ValidateMongoName(StocksCollectionName, nameof(StocksCollectionName));
        ValidateMongoName(UsersCollectionName, nameof(UsersCollectionName));
        ValidateMongoName(McpApiKeysCollectionName, nameof(McpApiKeysCollectionName));
        ValidateMongoName(McpAuditLogsCollectionName, nameof(McpAuditLogsCollectionName));
    }

    private static void ValidateMongoName(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length > 120 ||
            value.Contains('\0', StringComparison.Ordinal) ||
            value.Contains('$', StringComparison.Ordinal) ||
            value.Contains('/', StringComparison.Ordinal) ||
            value.Contains('\\', StringComparison.Ordinal) ||
            !MongoNameRegex().IsMatch(value))
        {
            throw new InvalidOperationException($"Configuracao MongoDB invalida: {parameterName}.");
        }
    }

    [GeneratedRegex(@"^[A-Za-z0-9_.-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex MongoNameRegex();
}
