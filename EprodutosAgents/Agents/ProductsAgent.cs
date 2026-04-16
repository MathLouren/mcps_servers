using System.Text.RegularExpressions;
using EprodutosAgents.Data;
using EprodutosAgents.Domain;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EprodutosAgents.Agents;

public sealed class ProductsAgent(EprodutosMongoContext context) : IProductsAgent
{
    public async Task<ProductDocument?> FindByPnAsync(string pn, CancellationToken cancellationToken = default)
    {
        var normalizedPn = RequireValue(pn, nameof(pn));
        var filter = ExactTextFilter(static product => product.Pn, normalizedPn);

        return await context.Products
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PagedResult<ProductDocument>> SearchAsync(
        ProductSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, criteria.Page);
        var pageSize = Math.Clamp(criteria.PageSize, 1, 100);
        var filter = BuildFilter(criteria);

        var totalTask = context.Products.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var itemsTask = context.Products
            .Find(filter)
            .SortBy(static product => product.Pn)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        await Task.WhenAll(totalTask, itemsTask);

        return new PagedResult<ProductDocument>(
            itemsTask.Result,
            totalTask.Result,
            page,
            pageSize);
    }

    private static FilterDefinition<ProductDocument> BuildFilter(ProductSearchCriteria criteria)
    {
        var builder = Builders<ProductDocument>.Filter;
        var filters = new List<FilterDefinition<ProductDocument>>();

        if (!string.IsNullOrWhiteSpace(criteria.SearchTerm))
        {
            var regex = ContainsRegex(criteria.SearchTerm);
            filters.Add(builder.Or(
                builder.Regex(static product => product.Pn, regex),
                builder.Regex(static product => product.Category, regex),
                builder.Regex(static product => product.Manufacturer, regex)));
        }

        if (!string.IsNullOrWhiteSpace(criteria.Pn))
        {
            filters.Add(ExactTextFilter(static product => product.Pn, criteria.Pn));
        }

        if (!string.IsNullOrWhiteSpace(criteria.Category))
        {
            filters.Add(builder.Regex(static product => product.Category, ContainsRegex(criteria.Category)));
        }

        if (!string.IsNullOrWhiteSpace(criteria.Manufacturer))
        {
            filters.Add(builder.Regex(static product => product.Manufacturer, ContainsRegex(criteria.Manufacturer)));
        }

        return filters.Count == 0 ? builder.Empty : builder.And(filters);
    }

    private static FilterDefinition<ProductDocument> ExactTextFilter(
        System.Linq.Expressions.Expression<Func<ProductDocument, string?>> field,
        string value)
    {
        return Builders<ProductDocument>.Filter.Regex(
            new ExpressionFieldDefinition<ProductDocument, string?>(field),
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
