using System.Globalization;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using EprodutosAgents.Domain;
using EprodutosAgents.Security;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EprodutosAgents.Data;

public sealed class EprodutosRepository(EprodutosMongoContext context)
{
    public Task<ProductDocument?> FindProductByPnAsync(
        string pn,
        CancellationToken cancellationToken = default)
    {
        return FindProductByPnAsync(pn, allowedManufacturers: null, cancellationToken);
    }

    public async Task<ProductDocument?> FindProductByPnAsync(
        string pn,
        IReadOnlyList<string>? allowedManufacturers,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<ProductDocument>.Filter;
        var filters = new List<FilterDefinition<ProductDocument>>
        {
            ExactTextFilter<ProductDocument>(
                static product => product.Pn,
                McpSecurityLimits.RequireExactText(pn, nameof(pn)))
        };

        var normalizedManufacturers = McpSecurityLimits.NormalizeScope(allowedManufacturers);
        if (normalizedManufacturers.Count > 0)
        {
            filters.Add(AnyExactTextFilter<ProductDocument>(
                static product => product.Manufacturer,
                normalizedManufacturers));
        }

        return await context.Products
            .Find(builder.And(filters))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PagedResult<ProductDocument>> SearchProductsAsync(
        ProductSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var page = McpSecurityLimits.NormalizePage(criteria.Page);
        var pageSize = McpSecurityLimits.NormalizePageSize(criteria.PageSize);
        var filter = BuildProductFilter(criteria);

        var totalTask = context.Products.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var itemsTask = context.Products
            .Find(filter)
            .SortBy(static product => product.Pn)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        await Task.WhenAll(totalTask, itemsTask);

        return new PagedResult<ProductDocument>(itemsTask.Result, totalTask.Result, page, pageSize);
    }

    public async Task<IReadOnlyList<StockWithProduct>> FindStocksByPnAsync(
        string pn,
        int limit = 50,
        IReadOnlyList<string>? allowedProcesses = null,
        IReadOnlyList<string>? allowedManufacturers = null,
        CancellationToken cancellationToken = default)
    {
        var stocks = await FindStockDocumentsByPnAsync(
            pn,
            limit,
            allowedProcesses,
            allowedManufacturers,
            cancellationToken);
        return await AttachProductsAsync(stocks, cancellationToken);
    }

    private async Task<IReadOnlyList<StockDocument>> FindStockDocumentsByPnAsync(
        string pn,
        int limit = 50,
        IReadOnlyList<string>? allowedProcesses = null,
        IReadOnlyList<string>? allowedManufacturers = null,
        CancellationToken cancellationToken = default)
    {
        var filter = await BuildStockFilterAsync(
            new StockSearchCriteria
            {
                Pn = pn,
                AllowedProcesses = allowedProcesses,
                AllowedManufacturers = allowedManufacturers
            },
            cancellationToken);

        return await context.Stocks
            .Find(filter)
            .SortByDescending(static stock => stock.Date)
            .Limit(Math.Clamp(limit, 1, 200))
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<StockWithProduct>> SearchStocksAsync(
        StockSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var page = McpSecurityLimits.NormalizePage(criteria.Page);
        var pageSize = McpSecurityLimits.NormalizePageSize(criteria.PageSize);
        var filter = await BuildStockFilterAsync(criteria, cancellationToken);

        var totalTask = context.Stocks.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var itemsTask = context.Stocks
            .Find(filter)
            .SortBy(static stock => stock.Pn)
            .ThenByDescending(static stock => stock.Date)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        await Task.WhenAll(totalTask, itemsTask);
        var items = await AttachProductsAsync(itemsTask.Result, cancellationToken);

        return new PagedResult<StockWithProduct>(items, totalTask.Result, page, pageSize);
    }

    public async Task<StockSummary> SummarizeStockByPnAsync(
        string pn,
        IReadOnlyList<string>? allowedProcesses = null,
        IReadOnlyList<string>? allowedManufacturers = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedPn = McpSecurityLimits.RequireExactText(pn, nameof(pn));
        var stocks = await FindStockDocumentsByPnAsync(
            normalizedPn,
            200,
            allowedProcesses,
            allowedManufacturers,
            cancellationToken);
        return BuildSummary(normalizedPn, stocks);
    }

    public async Task<ProductInventory> GetInventoryByPnAsync(
        string pn,
        int stockLimit = 100,
        IReadOnlyList<string>? allowedProcesses = null,
        IReadOnlyList<string>? allowedManufacturers = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedPn = McpSecurityLimits.RequireExactText(pn, nameof(pn));
        var safeStockLimit = Math.Clamp(stockLimit, 1, 200);

        var productTask = FindProductByPnAsync(normalizedPn, allowedManufacturers, cancellationToken);
        var stocksTask = FindStockDocumentsByPnAsync(
            normalizedPn,
            safeStockLimit,
            allowedProcesses,
            allowedManufacturers,
            cancellationToken);

        await Task.WhenAll(productTask, stocksTask);

        var product = productTask.Result;
        var stocks = stocksTask.Result.Select(stock => new StockWithProduct(stock, product)).ToArray();
        return new ProductInventory(
            normalizedPn,
            product,
            stocks,
            BuildSummary(normalizedPn, stocksTask.Result));
    }

    private static FilterDefinition<ProductDocument> BuildProductFilter(ProductSearchCriteria criteria)
    {
        var builder = Builders<ProductDocument>.Filter;
        var filters = new List<FilterDefinition<ProductDocument>>();
        var allowedManufacturers = McpSecurityLimits.NormalizeScope(criteria.AllowedManufacturers);

        if (!string.IsNullOrWhiteSpace(criteria.SearchTerm))
        {
            var regex = ContainsRegex(
                McpSecurityLimits.NormalizeOptionalSearchText(
                    criteria.SearchTerm,
                    nameof(criteria.SearchTerm))!);
            filters.Add(builder.Or(
                builder.Regex(static product => product.Pn, regex),
                builder.Regex(static product => product.Category, regex),
                builder.Regex(static product => product.Manufacturer, regex)));
        }

        if (!string.IsNullOrWhiteSpace(criteria.Pn))
        {
            filters.Add(ExactTextFilter<ProductDocument>(
                static product => product.Pn,
                McpSecurityLimits.RequireExactText(criteria.Pn, nameof(criteria.Pn))));
        }

        if (!string.IsNullOrWhiteSpace(criteria.Category))
        {
            filters.Add(builder.Regex(
                static product => product.Category,
                ContainsRegex(McpSecurityLimits.NormalizeOptionalSearchText(
                    criteria.Category,
                    nameof(criteria.Category))!)));
        }

        if (!string.IsNullOrWhiteSpace(criteria.Manufacturer))
        {
            filters.Add(builder.Regex(
                static product => product.Manufacturer,
                ContainsRegex(McpSecurityLimits.NormalizeOptionalSearchText(
                    criteria.Manufacturer,
                    nameof(criteria.Manufacturer))!)));
        }

        if (allowedManufacturers.Count > 0)
        {
            filters.Add(AnyExactTextFilter<ProductDocument>(
                static product => product.Manufacturer,
                allowedManufacturers));
        }

        return filters.Count == 0 ? builder.Empty : builder.And(filters);
    }

    private async Task<FilterDefinition<StockDocument>> BuildStockFilterAsync(
        StockSearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        var builder = Builders<StockDocument>.Filter;
        var filters = new List<FilterDefinition<StockDocument>>();
        var allowedProcesses = McpSecurityLimits.NormalizeScope(criteria.AllowedProcesses);
        var allowedManufacturers = McpSecurityLimits.NormalizeScope(criteria.AllowedManufacturers);

        if (!string.IsNullOrWhiteSpace(criteria.SearchTerm))
        {
            var searchTerm = McpSecurityLimits.NormalizeOptionalSearchText(
                criteria.SearchTerm,
                nameof(criteria.SearchTerm))!;
            var regex = ContainsRegex(searchTerm);
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

            var productPns = await FindProductPnsAsync(
                new ProductSearchCriteria
                {
                    SearchTerm = searchTerm,
                    AllowedManufacturers = allowedManufacturers
                },
                cancellationToken);

            if (productPns.Count > 0)
            {
                searchFilters.Add(builder.In(static stock => stock.Pn, productPns));
            }

            filters.Add(builder.Or(searchFilters));
        }

        if (!string.IsNullOrWhiteSpace(criteria.Category) ||
            !string.IsNullOrWhiteSpace(criteria.Manufacturer) ||
            allowedManufacturers.Count > 0)
        {
            var productPns = await FindProductPnsAsync(
                new ProductSearchCriteria
                {
                    Pn = criteria.Pn,
                    Category = criteria.Category,
                    Manufacturer = criteria.Manufacturer,
                    AllowedManufacturers = allowedManufacturers
                },
                cancellationToken);

            if (productPns.Count == 0)
            {
                return builder.Eq(static stock => stock.Id, "__no_matching_product__");
            }

            filters.Add(builder.In(static stock => stock.Pn, productPns));
        }

        if (!string.IsNullOrWhiteSpace(criteria.Pn))
        {
            filters.Add(ExactTextFilter<StockDocument>(
                static stock => stock.Pn,
                McpSecurityLimits.RequireExactText(criteria.Pn, nameof(criteria.Pn))));
        }

        if (!string.IsNullOrWhiteSpace(criteria.Uf))
        {
            filters.Add(ExactTextFilter<StockDocument>(
                static stock => stock.Uf,
                McpSecurityLimits.RequireExactText(criteria.Uf, nameof(criteria.Uf))));
        }

        if (!string.IsNullOrWhiteSpace(criteria.Cd))
        {
            filters.Add(ExactTextFilter<StockDocument>(
                static stock => stock.Cd,
                McpSecurityLimits.RequireExactText(criteria.Cd, nameof(criteria.Cd))));
        }

        if (criteria.AllowProcessSearch && !string.IsNullOrWhiteSpace(criteria.Process))
        {
            filters.Add(builder.Regex(
                static stock => stock.Process,
                ContainsRegex(McpSecurityLimits.NormalizeOptionalSearchText(
                    criteria.Process,
                    nameof(criteria.Process))!)));
        }

        if (allowedProcesses.Count > 0)
        {
            filters.Add(AnyExactTextFilter<StockDocument>(
                static stock => stock.Process,
                allowedProcesses));
        }

        if (criteria.Price.HasValue)
        {
            filters.Add(builder.Eq(static stock => stock.Price, criteria.Price.Value));
        }

        if (criteria.SalePrice.HasValue)
        {
            filters.Add(builder.Eq(static stock => stock.SalePrice, criteria.SalePrice.Value));
        }

        if (criteria.Status.HasValue)
        {
            filters.Add(builder.Eq(static stock => stock.Status, criteria.Status.Value));
        }

        if (!string.IsNullOrWhiteSpace(criteria.Validity))
        {
            filters.Add(ExactTextFilter<StockDocument>(
                static stock => stock.Validity,
                McpSecurityLimits.RequireExactText(criteria.Validity, nameof(criteria.Validity))));
        }

        return filters.Count == 0 ? builder.Empty : builder.And(filters);
    }

    private async Task<IReadOnlyList<StockWithProduct>> AttachProductsAsync(
        IReadOnlyList<StockDocument> stocks,
        CancellationToken cancellationToken)
    {
        var pns = stocks
            .Select(static stock => stock.Pn)
            .Where(static pn => !string.IsNullOrWhiteSpace(pn))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (pns.Length == 0)
        {
            return stocks.Select(static stock => new StockWithProduct(stock, null)).ToArray();
        }

        var products = await context.Products
            .Find(Builders<ProductDocument>.Filter.In(static product => product.Pn, pns))
            .ToListAsync(cancellationToken);

        var productsByPn = products
            .GroupBy(static product => product.Pn, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.OrdinalIgnoreCase);

        return stocks
            .Select(stock => new StockWithProduct(
                stock,
                productsByPn.TryGetValue(stock.Pn, out var product) ? product : null))
            .ToArray();
    }

    private async Task<IReadOnlyList<string>> FindProductPnsAsync(
        ProductSearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        var filter = BuildProductFilter(criteria);
        var products = await context.Products
            .Find(filter)
            .Project(static product => product.Pn)
            .Limit(McpSecurityLimits.MaxProductJoinPns + 1)
            .ToListAsync(cancellationToken);

        var distinctPns = products
            .Where(static pn => !string.IsNullOrWhiteSpace(pn))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (distinctPns.Length > McpSecurityLimits.MaxProductJoinPns)
        {
            throw new ArgumentException(
                "Filtro por produto muito amplo. Refine por PN, categoria ou fabricante.");
        }

        return distinctPns;
    }

    private static StockSummary BuildSummary(string pn, IReadOnlyList<StockDocument> stocks)
    {
        var salePrices = stocks
            .Where(static stock => stock.SalePrice.HasValue)
            .Select(static stock => stock.SalePrice!.Value)
            .ToArray();

        return new StockSummary(
            pn,
            stocks.Count,
            stocks.Sum(static stock => stock.Quantity ?? 0),
            salePrices.Length == 0 ? null : salePrices.Min(),
            salePrices.Length == 0 ? null : salePrices.Max(),
            stocks.FirstOrDefault(static stock => !string.IsNullOrWhiteSpace(stock.Currency))?.Currency,
            PickLatestValidity(stocks));
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

    private static FilterDefinition<TDocument> ExactTextFilter<TDocument>(
        Expression<Func<TDocument, string?>> field,
        string value)
    {
        return Builders<TDocument>.Filter.Regex(
            new ExpressionFieldDefinition<TDocument, string?>(field),
            new BsonRegularExpression(
                $"^{Regex.Escape(McpSecurityLimits.RequireExactText(value, nameof(value)))}$",
                "i"));
    }

    private static FilterDefinition<TDocument> AnyExactTextFilter<TDocument>(
        Expression<Func<TDocument, string?>> field,
        IReadOnlyList<string> values)
    {
        var builder = Builders<TDocument>.Filter;
        var fieldDefinition = new ExpressionFieldDefinition<TDocument, string?>(field);
        var filters = values
            .Select(value => builder.Regex(
                fieldDefinition,
                new BsonRegularExpression(
                    $"^{Regex.Escape(McpSecurityLimits.RequireExactText(value, nameof(value)))}$",
                    "i")))
            .ToArray();

        return filters.Length == 0 ? builder.Empty : builder.Or(filters);
    }

    private static BsonRegularExpression ContainsRegex(string value)
    {
        return new BsonRegularExpression(
            Regex.Escape(McpSecurityLimits.RequireExactText(value, nameof(value))),
            "i");
    }
}
