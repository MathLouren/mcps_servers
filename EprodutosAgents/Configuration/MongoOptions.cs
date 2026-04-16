using Microsoft.Extensions.Configuration;

namespace EprodutosAgents.Configuration;

public sealed record MongoOptions(
    string ConnectionString,
    string DatabaseName,
    string ProductsCollectionName,
    string StocksCollectionName)
{
    public const string SectionName = "MongoDb";

    public string EffectiveConnectionString => NormalizeMongoConnectionString(ConnectionString);

    public static MongoOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);

        return new MongoOptions(
            FirstNonEmpty(
                Environment.GetEnvironmentVariable("EPRODUTOS_MONGO_CONNECTION_STRING"),
                section["ConnectionString"],
                "mongodb://localhost:27017/eprodutos"),
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
                "stocks"));
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

    private static string NormalizeMongoConnectionString(string connectionString)
    {
        if (!Uri.TryCreate(connectionString, UriKind.Absolute, out var uri) ||
            !uri.Scheme.StartsWith("mongodb", StringComparison.OrdinalIgnoreCase))
        {
            return connectionString;
        }

        var pathParts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (pathParts.Length <= 1)
        {
            return connectionString;
        }

        var pathStart = connectionString.IndexOf(uri.AbsolutePath, StringComparison.Ordinal);
        if (pathStart < 0)
        {
            return connectionString;
        }

        return $"{connectionString[..pathStart]}/{pathParts[0]}{uri.Query}";
    }
}
