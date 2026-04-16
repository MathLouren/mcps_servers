namespace LocalMcpTester.Mcp;

public sealed record McpServerInfo(
    string Id,
    string Name,
    string Command,
    IReadOnlyList<string> Args,
    string WorkingDirectory,
    bool Running);
