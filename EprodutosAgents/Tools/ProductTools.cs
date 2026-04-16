using System.ComponentModel;
using EprodutosAgents.Agents;
using EprodutosAgents.Domain;
using ModelContextProtocol.Server;

namespace EprodutosAgents.Tools;

[McpServerToolType]
public sealed class ProductTools(IProductsAgent productsAgent)
{
    [McpServerTool]
    [Description("Busca produtos na collection products por PN, categoria e fabricante.")]
    public async Task<PagedResult<ProductResponse>> eprodutos_search_products(
        [Description("Texto livre para procurar em pn, category e manufacturer.")] string? search_term = null,
        [Description("PN exato do produto.")] string? pn = null,
        [Description("Categoria do produto, por exemplo Servidor.")] string? category = null,
        [Description("Fabricante do produto, por exemplo LENOVO DCG ESTOQUE.")] string? manufacturer = null,
        [Description("Pagina inicial em 1.")] int page = 1,
        [Description("Quantidade por pagina, de 1 a 100.")] int page_size = 20)
    {
        var result = await productsAgent.SearchAsync(new ProductSearchCriteria
        {
            SearchTerm = search_term,
            Pn = pn,
            Category = category,
            Manufacturer = manufacturer,
            Page = page,
            PageSize = page_size
        });

        return result.MapItems(static product => product.ToResponse());
    }

    [McpServerTool]
    [Description("Retorna um produto da collection products pelo PN exato.")]
    public async Task<ProductResponse?> eprodutos_get_product_by_pn(
        [Description("PN exato do produto.")] string pn)
    {
        var product = await productsAgent.FindByPnAsync(pn);
        return product?.ToResponse();
    }
}
