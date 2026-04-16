using EprodutosAgents.Domain;

namespace EprodutosAgents.Agents;

public interface ICatalogAgent
{
    Task<ProductInventoryResponse> GetInventoryByPnAsync(
        string pn,
        int stockLimit = 100,
        CancellationToken cancellationToken = default);
}
