using System.ComponentModel;
using System.Diagnostics;
using EprodutosAgents.Data;
using EprodutosAgents.Domain;
using EprodutosAgents.Security;
using ModelContextProtocol.Server;

namespace EprodutosAgents.Tools;

[McpServerToolType]
public sealed class ProductTools(
    EprodutosRepository repository,
    McpUserContext userContext,
    McpAuditLogService auditLogService)
{
    [McpServerTool]
    [Description("Busca produtos na collection products por PN, categoria e fabricante. Retorna uma tabela Markdown.")]
    public async Task<string> eprodutos_search_products(
        [Description("Texto livre para procurar em pn, category e manufacturer.")] string? search_term = null,
        [Description("PN exato do produto.")] string? pn = null,
        [Description("Categoria do produto, por exemplo Servidor.")] string? category = null,
        [Description("Fabricante do produto, por exemplo LENOVO DCG ESTOQUE.")] string? manufacturer = null,
        [Description("Pagina inicial em 1.")] int page = 1,
        [Description("Quantidade por pagina, de 1 a 100.")] int page_size = 20)
    {
        var user = userContext.GetRequiredUser();
        var stopwatch = Stopwatch.StartNew();
        var arguments = new Dictionary<string, object?>
        {
            ["search_term"] = search_term,
            ["pn"] = pn,
            ["category"] = category,
            ["manufacturer"] = manufacturer,
            ["page"] = page,
            ["page_size"] = page_size
        };

        try
        {
            var result = await repository.SearchProductsAsync(new ProductSearchCriteria
            {
                SearchTerm = search_term,
                Pn = pn,
                Category = category,
                Manufacturer = manufacturer,
                AllowedManufacturers = user.AllowedManufacturers,
                Page = page,
                PageSize = page_size
            });

            var response = result.MapItems(static product => product.ToResponse());
            var table = McpTableFormatter.Products(response);
            await auditLogService.RecordToolCallAsync(
                user,
                nameof(eprodutos_search_products),
                arguments,
                "success",
                stopwatch.ElapsedMilliseconds,
                response.Items.Count);

            return table;
        }
        catch (Exception ex)
        {
            await auditLogService.RecordToolCallAsync(
                user,
                nameof(eprodutos_search_products),
                arguments,
                "failed",
                stopwatch.ElapsedMilliseconds,
                errorMessage: ex.Message);
            if (McpToolException.CanExpose(ex))
            {
                throw;
            }

            throw new InvalidOperationException(McpToolException.GenericMessage);
        }
    }

    [McpServerTool]
    [Description("Retorna um produto da collection products pelo PN exato em tabela Markdown.")]
    public async Task<string> eprodutos_get_product_by_pn(
        [Description("PN exato do produto.")] string pn)
    {
        var user = userContext.GetRequiredUser();
        var stopwatch = Stopwatch.StartNew();
        var arguments = new Dictionary<string, object?> { ["pn"] = pn };

        try
        {
            var product = await repository.FindProductByPnAsync(
                pn,
                user.AllowedManufacturers);
            var response = product?.ToResponse();
            var table = McpTableFormatter.Product(response);
            await auditLogService.RecordToolCallAsync(
                user,
                nameof(eprodutos_get_product_by_pn),
                arguments,
                "success",
                stopwatch.ElapsedMilliseconds,
                response is null ? 0 : 1);

            return table;
        }
        catch (Exception ex)
        {
            await auditLogService.RecordToolCallAsync(
                user,
                nameof(eprodutos_get_product_by_pn),
                arguments,
                "failed",
                stopwatch.ElapsedMilliseconds,
                errorMessage: ex.Message);
            if (McpToolException.CanExpose(ex))
            {
                throw;
            }

            throw new InvalidOperationException(McpToolException.GenericMessage);
        }
    }
}
