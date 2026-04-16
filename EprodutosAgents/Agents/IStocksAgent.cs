using EprodutosAgents.Domain;

namespace EprodutosAgents.Agents;

public interface IStocksAgent
{
    Task<IReadOnlyList<StockDocument>> FindByPnAsync(
        string pn,
        int limit = 50,
        CancellationToken cancellationToken = default);

    Task<PagedResult<StockDocument>> SearchAsync(
        StockSearchCriteria criteria,
        CancellationToken cancellationToken = default);

    Task<StockSummary> SummarizeByPnAsync(
        string pn,
        CancellationToken cancellationToken = default);
}
