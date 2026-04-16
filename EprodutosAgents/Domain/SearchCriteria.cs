namespace EprodutosAgents.Domain;

public sealed record ProductSearchCriteria
{
    public string? SearchTerm { get; init; }

    public string? Pn { get; init; }

    public string? Category { get; init; }

    public string? Manufacturer { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}

public sealed record StockSearchCriteria
{
    public string? SearchTerm { get; init; }

    public string? Pn { get; init; }

    public string? Uf { get; init; }

    public string? Cd { get; init; }

    public string? Process { get; init; }

    public bool AllowProcessSearch { get; init; }

    public int? Status { get; init; }

    public string? Validity { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}
