using EprodutosAgents.Domain;

namespace EprodutosAgents.Agents;

public interface IProductsAgent
{
    Task<ProductDocument?> FindByPnAsync(string pn, CancellationToken cancellationToken = default);

    Task<PagedResult<ProductDocument>> SearchAsync(
        ProductSearchCriteria criteria,
        CancellationToken cancellationToken = default);
}
