using System.Text.Json.Serialization;

namespace EprodutosAgents.Domain;

public sealed record PagedResult<T>(
    [property: JsonPropertyName("items")] IReadOnlyList<T> Items,
    [property: JsonPropertyName("total")] long Total,
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("page_size")] int PageSize);

public sealed record ProductResponse(
    [property: JsonPropertyName("pn")] string Pn,
    [property: JsonPropertyName("category")] string? Category,
    [property: JsonPropertyName("manufacturer")] string? Manufacturer);

public sealed record CustomerStockResponse(
    [property: JsonPropertyName("pn")] string Pn,
    [property: JsonPropertyName("uf")] string? Uf,
    [property: JsonPropertyName("cd")] string? Cd,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("quantity")] int? Quantity,
    [property: JsonPropertyName("sale_price")] long? SalePrice,
    [property: JsonPropertyName("currency")] string? Currency,
    [property: JsonPropertyName("date")] DateTime? Date,
    [property: JsonPropertyName("status")] int? Status,
    [property: JsonPropertyName("validity")] string? Validity);

public sealed record EmployeeStockResponse(
    [property: JsonPropertyName("pn")] string Pn,
    [property: JsonPropertyName("uf")] string? Uf,
    [property: JsonPropertyName("cd")] string? Cd,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("process")] string? Process,
    [property: JsonPropertyName("quantity")] int? Quantity,
    [property: JsonPropertyName("price")] long? Price,
    [property: JsonPropertyName("sale_price")] long? SalePrice,
    [property: JsonPropertyName("currency")] string? Currency,
    [property: JsonPropertyName("date")] DateTime? Date,
    [property: JsonPropertyName("status")] int? Status,
    [property: JsonPropertyName("validity")] string? Validity,
    [property: JsonPropertyName("raw_excel_id")] string? RawExcelId,
    [property: JsonPropertyName("backlog")] object? Backlog,
    [property: JsonPropertyName("backlog_forecast")] object? BacklogForecast);

public sealed record StockSummary(
    [property: JsonPropertyName("pn")] string Pn,
    [property: JsonPropertyName("total_records")] long TotalRecords,
    [property: JsonPropertyName("total_quantity")] int TotalQuantity,
    [property: JsonPropertyName("min_sale_price")] long? MinSalePrice,
    [property: JsonPropertyName("max_sale_price")] long? MaxSalePrice,
    [property: JsonPropertyName("currency")] string? Currency,
    [property: JsonPropertyName("valid_until")] string? ValidUntil);

public sealed record ProductInventoryResponse(
    [property: JsonPropertyName("pn")] string Pn,
    [property: JsonPropertyName("product")] ProductDocument? Product,
    [property: JsonPropertyName("stocks")] IReadOnlyList<StockDocument> Stocks,
    [property: JsonPropertyName("summary")] StockSummary Summary);

public sealed record ProductInventoryResponse<TStock>(
    [property: JsonPropertyName("pn")] string Pn,
    [property: JsonPropertyName("category")] string? Category,
    [property: JsonPropertyName("manufacturer")] string? Manufacturer,
    [property: JsonPropertyName("stocks")] IReadOnlyList<TStock> Stocks,
    [property: JsonPropertyName("summary")] StockSummary Summary);

public static class ResponseMapper
{
    public static ProductResponse ToResponse(this ProductDocument product)
    {
        return new ProductResponse(
            product.Pn,
            product.Category,
            product.Manufacturer);
    }

    public static CustomerStockResponse ToCustomerResponse(this StockDocument stock)
    {
        return new CustomerStockResponse(
            stock.Pn,
            stock.Uf,
            stock.Cd,
            stock.Description,
            stock.Quantity,
            stock.SalePrice,
            stock.Currency,
            stock.Date,
            stock.Status,
            stock.Validity);
    }

    public static EmployeeStockResponse ToEmployeeResponse(this StockDocument stock)
    {
        return new EmployeeStockResponse(
            stock.Pn,
            stock.Uf,
            stock.Cd,
            stock.Description,
            stock.Process,
            stock.Quantity,
            stock.Price,
            stock.SalePrice,
            stock.Currency,
            stock.Date,
            stock.Status,
            stock.Validity,
            stock.RawExcelId,
            stock.Backlog,
            stock.BacklogForecast);
    }

    public static PagedResult<TOutput> MapItems<TInput, TOutput>(
        this PagedResult<TInput> result,
        Func<TInput, TOutput> map)
    {
        return new PagedResult<TOutput>(
            result.Items.Select(map).ToArray(),
            result.Total,
            result.Page,
            result.PageSize);
    }
}
