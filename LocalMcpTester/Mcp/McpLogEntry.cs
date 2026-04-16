namespace LocalMcpTester.Mcp;

public sealed record McpLogEntry(
    DateTimeOffset Timestamp,
    string Stream,
    string Message);
