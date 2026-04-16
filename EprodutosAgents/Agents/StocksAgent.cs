using System.Globalization;
using System.Text.RegularExpressions;
using EprodutosAgents.Data;
using EprodutosAgents.Domain;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EprodutosAgents.Agents;

public sealed class StocksAgent(EprodutosMongoContext context) : IStocksAgent
{
    public async Task<IReadOnlyList<StockDocument>> FindByPnAsync(
        string pn,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var normalizedPn = RequireValue(pn, nameof(pn));
        var safeLimit = Math.Clamp(limit, 1, 200);

        return await context.Stocks
            .Find(ExactTextFilter(static stock => stock.Pn, normalizedPn))
            .SortByDescending(static stock => stock.Date)
            .Limit(safeLimit)
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<StockDocument>> SearchAsync(
        StockSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, criteria.Page);
        var pageSize = Math.Clamp(criteria.PageSize, 1, 100);
        var filter = BuildFilter(criteria);

        var totalTask = context.Stocks.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var itemsTask = context.Stocks
            .Find(filter)
            .SortBy(static stock => stock.Pn)
            .ThenByDescending(static stock => stock.Date)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        await Task.WhenAll(totalTask, itemsTask);

        return new PagedResult<StockDocument>(
            itemsTask.Result,
            totalTask.Result,
            page,
            pageSize);
    }

    public async Task<StockSummary> SummarizeByPnAsync(
        string pn,
        CancellationToken cancellationToken = default)
    {
        var normalizedPn = RequireValue(pn, nameof(pn));
        var stocks = await FindByPnAsync(normalizedPn, 200, cancellationToken);
        var totalQuantity = stocks.Sum(static stock => stock.Quantity ?? 0);
        var salePrices = stocks
            .Where(static stock => stock.SalePrice.HasValue)
            .Select(static stock => stock.SalePrice!.Value)
            .ToArray();

        return new StockSummary(
            normalizedPn,
            stocks.Count,
            totalQuantity,
            salePrices.Length == 0 ? null : salePrices.Min(),
            salePrices.Length == 0 ? null : salePrices.Max(),
            stocks.FirstOrDefault(static stock => !string.IsNullOrWhiteSpace(stock.Currency))?.Currency,
            PickLatestValidity(stocks));
    }

    private static FilterDefinition<StockDocument> BuildFilter(StockSearchCriteria criteria)
    {
        var builder = Builders<StockDocument>.Filter;
        var filters = new List<FilterDefinition<StockDocument>>();

        if (!string.IsNullOrWhiteSpace(criteria.SearchTerm))
        {
            var regex = ContainsRegex(criteria.SearchTerm);
            var searchFilters = new List<FilterDefinition<StockDocument>>
            {
                builder.Regex(static stock => stock.Pn, regex),
                builder.Regex(static stock => stock.Description, regex),
                builder.Regex(static stock => stock.Uf, regex),
                builder.Regex(static stock => stock.Cd, regex),
                builder.Regex(static stock => stock.Validity, regex)
            };

            if (criteria.AllowProcessSearch)
            {
                searchFilters.Add(builder.Regex(static stock => stock.Process, regex));
            }

            filters.Add(builder.Or(searchFilters));
        }

        if (!string.IsNullOrWhiteSpace(criteria.Pn))
        {
            filters.Add(ExactTextFilter(static stock => stock.Pn, criteria.Pn));
        }

        if (!string.IsNullOrWhiteSpace(criteria.Uf))
        {
            filters.Add(ExactTextFilter(static stock => stock.Uf, criteria.Uf));
        }

        if (!string.IsNullOrWhiteSpace(criteria.Cd))
        {
            filters.Add(ExactTextFilter(static stock => stock.Cd, criteria.Cd));
        }

        if (criteria.AllowProcessSearch && !string.IsNullOrWhiteSpace(criteria.Process))
        {
            filters.Add(builder.Regex(static stock => stock.Process, ContainsRegex(criteria.Process)));
        }

        if (criteria.Status.HasValue)
        {
            filters.Add(builder.Eq(static stock => stock.Status, criteria.Status.Value));
        }

        if (!string.IsNullOrWhiteSpace(criteria.Validity))
        {
            filters.Add(ExactTextFilter(static stock => stock.Validity, criteria.Validity));
        }

        return filters.Count == 0 ? builder.Empty : builder.And(filters);
    }

    private static string? PickLatestValidity(IEnumerable<StockDocument> stocks)
    {
        return stocks
            .Select(static stock => stock.Validity)
            .Where(static validity => !string.IsNullOrWhiteSpace(validity))
            .Select(static validity => new
            {
                Original = validity!,
                Parsed = DateTime.TryParseExact(
                    validity,
                    "dd/MM/yyyy",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var date)
                    ? date
                    : (DateTime?)null
            })
            .OrderByDescending(static validity => validity.Parsed ?? DateTime.MinValue)
            .FirstOrDefault()?.Original;
    }

    private static FilterDefinition<StockDocument> ExactTextFilter(
        System.Linq.Expressions.Expression<Func<StockDocument, string?>> field,
        string value)
    {
        return Builders<StockDocument>.Filter.Regex(
            new ExpressionFieldDefinition<StockDocument, string?>(field),
            new BsonRegularExpression($"^{Regex.Escape(RequireValue(value, nameof(value)))}$", "i"));
    }

    private static BsonRegularExpression ContainsRegex(string value)
    {
        return new BsonRegularExpression(Regex.Escape(RequireValue(value, nameof(value))), "i");
    }

    private static string RequireValue(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Informe um valor valido.", parameterName);
        }

        return value.Trim();
    }
}
