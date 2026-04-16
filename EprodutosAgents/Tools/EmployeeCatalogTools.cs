using System.ComponentModel;
using EprodutosAgents.Agents;
using EprodutosAgents.Domain;
using ModelContextProtocol.Server;

namespace EprodutosAgents.Tools;

[McpServerToolType]
public sealed class EmployeeCatalogTools(ICatalogAgent catalogAgent)
{
    [McpServerTool]
    [Description("Relaciona products e stocks pelo PN e retorna cadastro, estoques completos e resumo de inventario para funcionario.")]
    public async Task<ProductInventoryResponse<EmployeeStockResponse>> eprodutos_get_product_inventory(
        [Description("PN exato para relacionar products.pn com stocks.pn.")] string pn,
        [Description("Limite de registros de estoque retornados, de 1 a 200.")] int stock_limit = 100)
    {
        var inventory = await catalogAgent.GetInventoryByPnAsync(pn, stock_limit);

        return new ProductInventoryResponse<EmployeeStockResponse>(
            inventory.Pn,
            inventory.Product?.Category,
            inventory.Product?.Manufacturer,
            inventory.Stocks.Select(static stock => stock.ToEmployeeResponse()).ToArray(),
            inventory.Summary);
    }
}
