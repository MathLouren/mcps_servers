using EprodutosAgents.Domain;

namespace EprodutosAgents.Agents;

public sealed class CatalogAgent(IProductsAgent productsAgent, IStocksAgent stocksAgent) : ICatalogAgent
{
    public async Task<ProductInventoryResponse> GetInventoryByPnAsync(
        string pn,
        int stockLimit = 100,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pn))
        {
            throw new ArgumentException("Informe um PN valido.", nameof(pn));
        }

        var normalizedPn = pn.Trim();
        var safeStockLimit = Math.Clamp(stockLimit, 1, 200);

        var productTask = productsAgent.FindByPnAsync(normalizedPn, cancellationToken);
        var stocksTask = stocksAgent.FindByPnAsync(normalizedPn, safeStockLimit, cancellationToken);
        var summaryTask = stocksAgent.SummarizeByPnAsync(normalizedPn, cancellationToken);

        await Task.WhenAll(productTask, stocksTask, summaryTask);

        return new ProductInventoryResponse(
            normalizedPn,
            productTask.Result,
            stocksTask.Result,
            summaryTask.Result);
    }
}
