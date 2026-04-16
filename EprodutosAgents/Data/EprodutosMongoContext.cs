using EprodutosAgents.Configuration;
using EprodutosAgents.Domain;
using MongoDB.Driver;

namespace EprodutosAgents.Data;

public sealed class EprodutosMongoContext
{
    public EprodutosMongoContext(MongoOptions options)
    {
        var client = new MongoClient(options.EffectiveConnectionString);
        Database = client.GetDatabase(options.DatabaseName);
        Products = Database.GetCollection<ProductDocument>(options.ProductsCollectionName);
        Stocks = Database.GetCollection<StockDocument>(options.StocksCollectionName);
    }

    public IMongoDatabase Database { get; }

    public IMongoCollection<ProductDocument> Products { get; }

    public IMongoCollection<StockDocument> Stocks { get; }
}
