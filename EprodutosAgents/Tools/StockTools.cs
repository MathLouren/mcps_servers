using System.ComponentModel;
using System.Diagnostics;
using EprodutosAgents.Data;
using EprodutosAgents.Domain;
using EprodutosAgents.Security;
using ModelContextProtocol.Server;

namespace EprodutosAgents.Tools;

[McpServerToolType]
public sealed class StockTools(
    EprodutosRepository repository,
    McpUserContext userContext,
    McpAuditLogService auditLogService)
{
    [McpServerTool]
    [Description("Busca estoques em tabela Markdown. Admin, gerente e funcionario recebem price e sale_price; cliente recebe somente sale_price.")]
    public async Task<string> eprodutos_search_stocks(
        [Description("Texto livre para procurar em pn, description, uf, cd e validity. Para funcionario tambem procura em process.")] string? search_term = null,
        [Description("PN exato do estoque.")] string? pn = null,
        [Description("Categoria do produto na collection products, por exemplo Componente.")] string? category = null,
        [Description("Fabricante do produto na collection products, por exemplo PATRIOT.")] string? manufacturer = null,
        [Description("UF exata, por exemplo ES.")] string? uf = null,
        [Description("CD exato, por exemplo ES.")] string? cd = null,
        [Description("Nome ou trecho do processo. Permitido apenas para admin, gerente e funcionario.")] string? process = null,
        [Description("Preco de custo exato salvo em price. Permitido apenas para admin, gerente e funcionario.")] long? price = null,
        [Description("Preco de venda exato salvo em sale_price.")] long? sale_price = null,
        [Description("Status numerico do registro.")] int? status = null,
        [Description("Validade exata no formato salvo no MongoDB, por exemplo 14/01/2026.")] string? validity = null,
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
            ["uf"] = uf,
            ["cd"] = cd,
            ["process"] = process,
            ["price"] = price,
            ["sale_price"] = sale_price,
            ["status"] = status,
            ["validity"] = validity,
            ["page"] = page,
            ["page_size"] = page_size
        };

        try
        {
            EnsureAllowedPrivateFilters(user, process, price);

            var result = await repository.SearchStocksAsync(new StockSearchCriteria
            {
                SearchTerm = search_term,
                Pn = pn,
                Category = category,
                Manufacturer = manufacturer,
                Uf = uf,
                Cd = cd,
                Process = user.CanSeeCostPrice ? process : null,
                AllowProcessSearch = user.CanSeeCostPrice,
                Price = user.CanSeeCostPrice ? price : null,
                SalePrice = sale_price,
                Status = status,
                Validity = validity,
                AllowedProcesses = user.AllowedProcesses,
                AllowedManufacturers = user.AllowedManufacturers,
                Page = page,
                PageSize = page_size
            });

            var response = user.CanSeeCostPrice
                ? McpTableFormatter.EmployeeStocks(result.MapItems(static stock => stock.ToEmployeeResponse()))
                : McpTableFormatter.CustomerStocks(result.MapItems(static stock => stock.ToCustomerResponse()));

            await auditLogService.RecordToolCallAsync(
                user,
                nameof(eprodutos_search_stocks),
                arguments,
                "success",
                stopwatch.ElapsedMilliseconds,
                result.Items.Count);

            return response;
        }
        catch (Exception ex)
        {
            await auditLogService.RecordToolCallAsync(
                user,
                nameof(eprodutos_search_stocks),
                arguments,
                ex is UnauthorizedAccessException ? "forbidden" : "failed",
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
    [Description("Busca inventario cruzando products e stocks pelo PN. Admin, gerente e funcionario recebem price, process e sale_price; cliente recebe somente campos seguros.")]
    public async Task<string> eprodutos_search_inventory(
        [Description("Texto livre para procurar em pn, description, uf, cd, validity e campos relacionados de products. Para funcionario tambem procura em process.")] string? search_term = null,
        [Description("UF exata, por exemplo ES.")] string? uf = null,
        [Description("CD exato, por exemplo ES.")] string? cd = null,
        [Description("Nome ou trecho do processo. Permitido apenas para admin, gerente e funcionario.")] string? process = null,
        [Description("Categoria do produto na collection products, por exemplo Componente.")] string? category = null,
        [Description("Pagina inicial em 1.")] int page = 1,
        [Description("Quantidade por pagina, de 1 a 100.")] int page_size = 20)
    {
        var user = userContext.GetRequiredUser();
        var stopwatch = Stopwatch.StartNew();
        var arguments = new Dictionary<string, object?>
        {
            ["search_term"] = search_term,
            ["uf"] = uf,
            ["cd"] = cd,
            ["process"] = process,
            ["category"] = category,
            ["page"] = page,
            ["page_size"] = page_size
        };

        try
        {
            EnsureAllowedPrivateFilters(user, process, price: null);

            var result = await repository.SearchStocksAsync(new StockSearchCriteria
            {
                SearchTerm = search_term,
                Category = category,
                Uf = uf,
                Cd = cd,
                Process = user.CanSeeCostPrice ? process : null,
                AllowProcessSearch = user.CanSeeCostPrice,
                AllowedProcesses = user.AllowedProcesses,
                AllowedManufacturers = user.AllowedManufacturers,
                Page = page,
                PageSize = page_size
            });

            var response = user.CanSeeCostPrice
                ? McpTableFormatter.EmployeeStocks(result.MapItems(static stock => stock.ToEmployeeResponse()))
                : McpTableFormatter.CustomerStocks(result.MapItems(static stock => stock.ToCustomerResponse()));

            await auditLogService.RecordToolCallAsync(
                user,
                nameof(eprodutos_search_inventory),
                arguments,
                "success",
                stopwatch.ElapsedMilliseconds,
                result.Items.Count);

            return response;
        }
        catch (Exception ex)
        {
            await auditLogService.RecordToolCallAsync(
                user,
                nameof(eprodutos_search_inventory),
                arguments,
                ex is UnauthorizedAccessException ? "forbidden" : "failed",
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
    [Description("Retorna os estoques mais recentes de um PN em tabela Markdown. Admin, gerente e funcionario recebem price; cliente nao recebe price.")]
    public async Task<string> eprodutos_get_stocks_by_pn(
        [Description("PN exato do estoque.")] string pn,
        [Description("Limite de registros retornados, de 1 a 200.")] int limit = 50)
    {
        var user = userContext.GetRequiredUser();
        var stopwatch = Stopwatch.StartNew();
        var arguments = new Dictionary<string, object?> { ["pn"] = pn, ["limit"] = limit };

        try
        {
            var stocks = await repository.FindStocksByPnAsync(
                pn,
                limit,
                user.AllowedProcesses,
                user.AllowedManufacturers);
            var response = user.CanSeeCostPrice
                ? McpTableFormatter.EmployeeStocks(stocks.Select(static stock => stock.ToEmployeeResponse()).ToArray())
                : McpTableFormatter.CustomerStocks(stocks.Select(static stock => stock.ToCustomerResponse()).ToArray());

            await auditLogService.RecordToolCallAsync(
                user,
                nameof(eprodutos_get_stocks_by_pn),
                arguments,
                "success",
                stopwatch.ElapsedMilliseconds,
                stocks.Count);

            return response;
        }
        catch (Exception ex)
        {
            await auditLogService.RecordToolCallAsync(
                user,
                nameof(eprodutos_get_stocks_by_pn),
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
    [Description("Resume quantidade, faixa de preco de venda e validade dos estoques de um PN em tabela Markdown.")]
    public async Task<string> eprodutos_summarize_stock_by_pn(
        [Description("PN exato do estoque.")] string pn)
    {
        var user = userContext.GetRequiredUser();
        var stopwatch = Stopwatch.StartNew();
        var arguments = new Dictionary<string, object?> { ["pn"] = pn };

        try
        {
            var summary = await repository.SummarizeStockByPnAsync(
                pn,
                user.AllowedProcesses,
                user.AllowedManufacturers);
            var table = McpTableFormatter.Summary(summary);
            await auditLogService.RecordToolCallAsync(
                user,
                nameof(eprodutos_summarize_stock_by_pn),
                arguments,
                "success",
                stopwatch.ElapsedMilliseconds,
                summary.TotalRecords);

            return table;
        }
        catch (Exception ex)
        {
            await auditLogService.RecordToolCallAsync(
                user,
                nameof(eprodutos_summarize_stock_by_pn),
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
    [Description("Relaciona products e stocks pelo PN em tabelas Markdown. O retorno de estoque muda conforme a role do usuario autenticado.")]
    public async Task<string> eprodutos_get_product_inventory(
        [Description("PN exato para relacionar products.pn com stocks.pn.")] string pn,
        [Description("Limite de registros de estoque retornados, de 1 a 200.")] int stock_limit = 100)
    {
        var user = userContext.GetRequiredUser();
        var stopwatch = Stopwatch.StartNew();
        var arguments = new Dictionary<string, object?> { ["pn"] = pn, ["stock_limit"] = stock_limit };

        try
        {
            var inventory = await repository.GetInventoryByPnAsync(
                pn,
                stock_limit,
                user.AllowedProcesses,
                user.AllowedManufacturers);
            var response = user.CanSeeCostPrice
                ? McpTableFormatter.EmployeeInventory(new ProductInventoryResponse<EmployeeStockResponse>(
                    inventory.Pn,
                    inventory.Product?.Category,
                    inventory.Product?.Manufacturer,
                    inventory.Stocks.Select(static stock => stock.ToEmployeeResponse()).ToArray(),
                    inventory.Summary))
                : McpTableFormatter.CustomerInventory(new ProductInventoryResponse<CustomerStockResponse>(
                    inventory.Pn,
                    inventory.Product?.Category,
                    inventory.Product?.Manufacturer,
                    inventory.Stocks.Select(static stock => stock.ToCustomerResponse()).ToArray(),
                    inventory.Summary));

            await auditLogService.RecordToolCallAsync(
                user,
                nameof(eprodutos_get_product_inventory),
                arguments,
                "success",
                stopwatch.ElapsedMilliseconds,
                inventory.Stocks.Count);

            return response;
        }
        catch (Exception ex)
        {
            await auditLogService.RecordToolCallAsync(
                user,
                nameof(eprodutos_get_product_inventory),
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

    private static void EnsureAllowedPrivateFilters(
        AuthenticatedMcpUser user,
        string? process,
        long? price)
    {
        if (user.CanSeeCostPrice)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(process))
        {
            throw new UnauthorizedAccessException("Seu perfil nao pode filtrar por process.");
        }

        if (price.HasValue)
        {
            throw new UnauthorizedAccessException("Seu perfil nao pode filtrar por price.");
        }
    }
}
