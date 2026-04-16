using System.Text.Json;

namespace LocalMcpTester.Mcp;

public sealed class McpRpcException : Exception
{
    public McpRpcException(string message, JsonElement? error = null)
        : base(message)
    {
        Error = error;
    }

    public JsonElement? Error { get; }
}
