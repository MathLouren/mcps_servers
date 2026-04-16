using System.ComponentModel;
using EprodutosAgents.Agents;
using EprodutosAgents.Domain;
using ModelContextProtocol.Server;

namespace EprodutosAgents.Tools;

[McpServerToolType]
public sealed class EmployeeStockTools(IStocksAgent stocksAgent)
{
    [McpServerTool]
    [Description("Busca registros na collection stocks com acesso completo de funcionario: PN, descricao, processo, UF, CD, status e validade.")]
    public async Task<PagedResult<EmployeeStockResponse>> eprodutos_search_stocks(
        [Description("Texto livre para procurar em pn, description, process, uf, cd e validity.")] string? search_term = null,
        [Description("PN exato do estoque.")] string? pn = null,
        [Description("UF exata, por exemplo ES.")] string? uf = null,
        [Description("CD exato, por exemplo ES.")] string? cd = null,
        [Description("Nome ou trecho do processo, por exemplo Tabela Mazer - Lenovo ISG.")] string? process = null,
        [Description("Status numerico do registro.")] int? status = null,
        [Description("Validade exata no formato salvo no MongoDB, por exemplo 14/01/2026.")] string? validity = null,
        [Description("Pagina inicial em 1.")] int page = 1,
        [Description("Quantidade por pagina, de 1 a 100.")] int page_size = 20)
    {
        var result = await stocksAgent.SearchAsync(new StockSearchCriteria
        {
            SearchTerm = search_term,
            Pn = pn,
            Uf = uf,
            Cd = cd,
            Process = process,
            AllowProcessSearch = true,
            Status = status,
            Validity = validity,
            Page = page,
            PageSize = page_size
        });

        return result.MapItems(static stock => stock.ToEmployeeResponse());
    }

    [McpServerTool]
    [Description("Retorna os registros mais recentes da collection stocks para um PN exato com acesso completo de funcionario.")]
    public async Task<IReadOnlyList<EmployeeStockResponse>> eprodutos_get_stocks_by_pn(
        [Description("PN exato do estoque.")] string pn,
        [Description("Limite de registros retornados, de 1 a 200.")] int limit = 50)
    {
        var stocks = await stocksAgent.FindByPnAsync(pn, limit);
        return stocks.Select(static stock => stock.ToEmployeeResponse()).ToArray();
    }

    [McpServerTool]
    [Description("Resume quantidade, faixa de preco de venda e validade dos estoques de um PN.")]
    public Task<StockSummary> eprodutos_summarize_stock_by_pn(
        [Description("PN exato do estoque.")] string pn)
    {
        return stocksAgent.SummarizeByPnAsync(pn);
    }
}
