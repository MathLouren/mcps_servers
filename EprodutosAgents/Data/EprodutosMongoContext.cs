using EprodutosAgents.Configuration;
using EprodutosAgents.Domain;
using MongoDB.Driver;

namespace EprodutosAgents.Data;

public sealed class EprodutosMongoContext
{
    public EprodutosMongoContext(MongoOptions options)
    {
        var client = new MongoClient(options.ConnectionString);
        Database = client.GetDatabase(options.DatabaseName);
        Products = Database.GetCollection<ProductDocument>(options.ProductsCollectionName);
        Stocks = Database.GetCollection<StockDocument>(options.StocksCollectionName);
        Users = Database.GetCollection<UserDocument>(options.UsersCollectionName);
        McpApiKeys = Database.GetCollection<McpApiKeyDocument>(options.McpApiKeysCollectionName);
        McpAuditLogs = Database.GetCollection<McpAuditLogDocument>(options.McpAuditLogsCollectionName);
    }

    public IMongoDatabase Database { get; }

    public IMongoCollection<ProductDocument> Products { get; }

    public IMongoCollection<StockDocument> Stocks { get; }

    public IMongoCollection<UserDocument> Users { get; }

    public IMongoCollection<McpApiKeyDocument> McpApiKeys { get; }

    public IMongoCollection<McpAuditLogDocument> McpAuditLogs { get; }

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        await Products.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<ProductDocument>(
                    Builders<ProductDocument>.IndexKeys.Ascending(static product => product.Pn)),
                new CreateIndexModel<ProductDocument>(
                    Builders<ProductDocument>.IndexKeys.Ascending(static product => product.Category)),
                new CreateIndexModel<ProductDocument>(
                    Builders<ProductDocument>.IndexKeys.Ascending(static product => product.Manufacturer))
            ],
            cancellationToken: cancellationToken);

        await Stocks.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<StockDocument>(
                    Builders<StockDocument>.IndexKeys
                        .Ascending(static stock => stock.Pn)
                        .Descending(static stock => stock.Date)),
                new CreateIndexModel<StockDocument>(
                    Builders<StockDocument>.IndexKeys
                        .Ascending(static stock => stock.Uf)
                        .Ascending(static stock => stock.Cd)),
                new CreateIndexModel<StockDocument>(
                    Builders<StockDocument>.IndexKeys.Ascending(static stock => stock.Status))
            ],
            cancellationToken: cancellationToken);

        await Users.Indexes.CreateOneAsync(
            new CreateIndexModel<UserDocument>(
                Builders<UserDocument>.IndexKeys.Ascending(static user => user.Email)),
            cancellationToken: cancellationToken);

        await McpApiKeys.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<McpApiKeyDocument>(
                    Builders<McpApiKeyDocument>.IndexKeys.Ascending(static key => key.KeyId),
                    new CreateIndexOptions { Unique = true }),
                new CreateIndexModel<McpApiKeyDocument>(
                    Builders<McpApiKeyDocument>.IndexKeys
                        .Ascending(static key => key.UserId)
                        .Ascending(static key => key.RevokedAt)),
                new CreateIndexModel<McpApiKeyDocument>(
                    Builders<McpApiKeyDocument>.IndexKeys.Ascending(static key => key.UserEmail)),
                new CreateIndexModel<McpApiKeyDocument>(
                    Builders<McpApiKeyDocument>.IndexKeys.Ascending(static key => key.ExpiresAt))
            ],
            cancellationToken: cancellationToken);

        await McpAuditLogs.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<McpAuditLogDocument>(
                    Builders<McpAuditLogDocument>.IndexKeys.Descending(static log => log.CreatedAt)),
                new CreateIndexModel<McpAuditLogDocument>(
                    Builders<McpAuditLogDocument>.IndexKeys.Ascending(static log => log.UserId)),
                new CreateIndexModel<McpAuditLogDocument>(
                    Builders<McpAuditLogDocument>.IndexKeys.Ascending(static log => log.UserEmail)),
                new CreateIndexModel<McpAuditLogDocument>(
                    Builders<McpAuditLogDocument>.IndexKeys.Ascending(static log => log.ToolName)),
                new CreateIndexModel<McpAuditLogDocument>(
                    Builders<McpAuditLogDocument>.IndexKeys.Ascending(static log => log.ApiKeyId))
            ],
            cancellationToken: cancellationToken);
    }
}
