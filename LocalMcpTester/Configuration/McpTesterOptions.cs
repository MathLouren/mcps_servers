namespace LocalMcpTester.Configuration;

public sealed class McpTesterOptions
{
    public const string SectionName = "McpTester";

    public int DefaultRequestTimeoutSeconds { get; init; } = 30;

    public List<McpServerDefinition> Servers { get; init; } = [];
}

public sealed class McpServerDefinition
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Command { get; init; } = string.Empty;

    public string[] Args { get; init; } = [];

    public string? WorkingDirectory { get; init; }

    public Dictionary<string, string?> Environment { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public int? RequestTimeoutSeconds { get; init; }
}
