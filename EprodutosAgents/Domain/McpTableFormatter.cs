using System.Globalization;
using System.Text;
using EprodutosAgents.Security;

namespace EprodutosAgents.Domain;

public static class McpTableFormatter
{
    public static string Products(PagedResult<ProductResponse> result)
    {
        var builder = new StringBuilder();
        AppendPagedInfo(builder, result.Total, result.Page, result.PageSize, result.Items.Count);
        AppendTable(
            builder,
            ["pn", "category", "manufacturer"],
            result.Items.Select(static product => new object?[]
            {
                product.Pn,
                product.Category,
                product.Manufacturer
            }));

        return builder.ToString();
    }

    public static string Product(ProductResponse? product)
    {
        if (product is null)
        {
            return Empty("produto");
        }

        return SingleRow(
            ["pn", "category", "manufacturer"],
            [product.Pn, product.Category, product.Manufacturer]);
    }

    public static string EmployeeStocks(PagedResult<EmployeeStockResponse> result)
    {
        var builder = new StringBuilder();
        AppendPagedInfo(builder, result.Total, result.Page, result.PageSize, result.Items.Count);
        AppendEmployeeStocksTable(builder, result.Items);
        return builder.ToString();
    }

    public static string CustomerStocks(PagedResult<CustomerStockResponse> result)
    {
        var builder = new StringBuilder();
        AppendPagedInfo(builder, result.Total, result.Page, result.PageSize, result.Items.Count);
        AppendCustomerStocksTable(builder, result.Items);
        return builder.ToString();
    }

    public static string EmployeeStocks(IReadOnlyList<EmployeeStockResponse> stocks)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Registros: {stocks.Count}");
        builder.AppendLine();
        AppendEmployeeStocksTable(builder, stocks);
        return builder.ToString();
    }

    public static string CustomerStocks(IReadOnlyList<CustomerStockResponse> stocks)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Registros: {stocks.Count}");
        builder.AppendLine();
        AppendCustomerStocksTable(builder, stocks);
        return builder.ToString();
    }

    public static string Summary(StockSummary summary)
    {
        return SingleRow(
            ["pn", "total_records", "total_quantity", "min_sale_price", "max_sale_price", "currency", "valid_until"],
            [
                summary.Pn,
                summary.TotalRecords,
                summary.TotalQuantity,
                summary.MinSalePrice,
                summary.MaxSalePrice,
                summary.Currency,
                summary.ValidUntil
            ]);
    }

    public static string EmployeeInventory(ProductInventoryResponse<EmployeeStockResponse> inventory)
    {
        var builder = new StringBuilder();
        AppendInventoryHeader(builder, inventory.Pn, inventory.Category, inventory.Manufacturer);
        builder.AppendLine("Resumo");
        builder.AppendLine();
        builder.AppendLine(Summary(inventory.Summary));
        builder.AppendLine();
        builder.AppendLine("Estoques");
        builder.AppendLine();
        AppendEmployeeStocksTable(builder, inventory.Stocks);
        return builder.ToString();
    }

    public static string CustomerInventory(ProductInventoryResponse<CustomerStockResponse> inventory)
    {
        var builder = new StringBuilder();
        AppendInventoryHeader(builder, inventory.Pn, inventory.Category, inventory.Manufacturer);
        builder.AppendLine("Resumo");
        builder.AppendLine();
        builder.AppendLine(Summary(inventory.Summary));
        builder.AppendLine();
        builder.AppendLine("Estoques");
        builder.AppendLine();
        AppendCustomerStocksTable(builder, inventory.Stocks);
        return builder.ToString();
    }

    private static void AppendInventoryHeader(
        StringBuilder builder,
        string pn,
        string? category,
        string? manufacturer)
    {
        builder.AppendLine("Produto");
        builder.AppendLine();
        AppendTable(
            builder,
            ["pn", "category", "manufacturer"],
            [new object?[] { pn, category, manufacturer }]);
        builder.AppendLine();
    }

    private static void AppendEmployeeStocksTable(
        StringBuilder builder,
        IReadOnlyList<EmployeeStockResponse> stocks)
    {
        AppendTable(
            builder,
            ["pn", "category", "manufacturer", "uf", "cd", "description", "process", "quantity", "price", "sale_price", "currency", "date", "status", "validity"],
            stocks.Select(static stock => new object?[]
            {
                stock.Pn,
                stock.Category,
                stock.Manufacturer,
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
                stock.Validity
            }));
    }

    private static void AppendCustomerStocksTable(
        StringBuilder builder,
        IReadOnlyList<CustomerStockResponse> stocks)
    {
        AppendTable(
            builder,
            ["pn", "category", "manufacturer", "uf", "cd", "description", "quantity", "sale_price", "currency", "date", "status", "validity"],
            stocks.Select(static stock => new object?[]
            {
                stock.Pn,
                stock.Category,
                stock.Manufacturer,
                stock.Uf,
                stock.Cd,
                stock.Description,
                stock.Quantity,
                stock.SalePrice,
                stock.Currency,
                stock.Date,
                stock.Status,
                stock.Validity
            }));
    }

    private static string SingleRow(IReadOnlyList<string> headers, IReadOnlyList<object?> values)
    {
        var builder = new StringBuilder();
        AppendTable(builder, headers, [values]);
        return builder.ToString();
    }

    private static string Empty(string entity)
    {
        return SingleRow(["resultado"], [$"Nenhum {entity} encontrado."]);
    }

    private static void AppendPagedInfo(
        StringBuilder builder,
        long total,
        int page,
        int pageSize,
        int returned)
    {
        AppendTable(
            builder,
            ["total", "page", "page_size", "returned"],
            [new object?[] { total, page, pageSize, returned }]);
        builder.AppendLine();
    }

    private static void AppendTable(
        StringBuilder builder,
        IReadOnlyList<string> headers,
        IEnumerable<IReadOnlyList<object?>> rows)
    {
        builder.Append('|');
        foreach (var header in headers)
        {
            builder.Append(' ').Append(header).Append(" |");
        }

        builder.AppendLine();
        builder.Append('|');
        foreach (var _ in headers)
        {
            builder.Append(" --- |");
        }

        builder.AppendLine();

        var wroteRow = false;
        foreach (var row in rows)
        {
            wroteRow = true;
            builder.Append('|');
            foreach (var value in row)
            {
                builder.Append(' ').Append(Format(value)).Append(" |");
            }

            builder.AppendLine();
        }

        if (!wroteRow)
        {
            builder.Append('|');
            foreach (var _ in headers)
            {
                builder.Append("  |");
            }

            builder.AppendLine();
        }
    }

    private static string Format(object? value)
    {
        return value switch
        {
            null => "",
            string text => Escape(text),
            DateTime date => date.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fff+00:00", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? "",
            _ => Escape(value.ToString() ?? "")
        };
    }

    private static string Escape(string value)
    {
        return McpSecurityLimits.SanitizeMarkdownCell(value);
    }
}
