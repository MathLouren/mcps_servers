using System.ComponentModel;
using EprodutosAgents.Agents;
using EprodutosAgents.Domain;
using ModelContextProtocol.Server;

namespace EprodutosAgents.Tools;

[McpServerToolType]
public sealed class CustomerCatalogTools(ICatalogAgent catalogAgent)
{
    [McpServerTool]
    [Description("Relaciona products e stocks pelo PN e retorna cadastro, estoques permitidos e resumo de inventario para cliente.")]
    public async Task<ProductInventoryResponse<CustomerStockResponse>> eprodutos_get_product_inventory(
        [Description("PN exato para relacionar products.pn com stocks.pn.")] string pn,
        [Description("Limite de registros de estoque retornados, de 1 a 200.")] int stock_limit = 100)
    {
        var inventory = await catalogAgent.GetInventoryByPnAsync(pn, stock_limit);

        return new ProductInventoryResponse<CustomerStockResponse>(
            inventory.Pn,
            inventory.Product?.Category,
            inventory.Product?.Manufacturer,
            inventory.Stocks.Select(static stock => stock.ToCustomerResponse()).ToArray(),
            inventory.Summary);
    }
}
