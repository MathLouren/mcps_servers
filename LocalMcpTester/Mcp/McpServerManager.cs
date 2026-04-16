using System.Collections.Concurrent;
using System.Text.Json;
using LocalMcpTester.Configuration;
using Microsoft.Extensions.Options;

namespace LocalMcpTester.Mcp;

public sealed class McpServerManager : IAsyncDisposable
{
    private readonly IReadOnlyDictionary<string, McpServerDefinition> definitions;
    private readonly ConcurrentDictionary<string, McpStdioClient> clients = new(StringComparer.OrdinalIgnoreCase);
    private readonly WorkspacePaths paths;
    private readonly ILoggerFactory loggerFactory;
    private readonly int defaultRequestTimeoutSeconds;

    public McpServerManager(
        IOptions<McpTesterOptions> options,
        WorkspacePaths paths,
        ILoggerFactory loggerFactory)
    {
        this.paths = paths;
        this.loggerFactory = loggerFactory;
        defaultRequestTimeoutSeconds = Math.Max(1, options.Value.DefaultRequestTimeoutSeconds);

        definitions = options.Value.Servers
            .Where(static server => !string.IsNullOrWhiteSpace(server.Id))
            .ToDictionary(
                static server => server.Id,
                static server => server,
                StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<McpServerInfo> ListServers()
    {
        return definitions.Values
            .OrderBy(static server => server.Id, StringComparer.OrdinalIgnoreCase)
            .Select(ToInfo)
            .ToArray();
    }

    public McpServerInfo GetServer(string serverId)
    {
        return ToInfo(GetDefinition(serverId));
    }

    public IReadOnlyList<McpLogEntry> GetLogs(string serverId)
    {
        GetDefinition(serverId);
        return clients.TryGetValue(serverId, out var client)
            ? client.GetLogs()
            : [];
    }

    public async Task StartAsync(string serverId, CancellationToken cancellationToken)
    {
        var client = GetOrCreateClient(serverId);
        await client.StartAsync(cancellationToken);
    }

    public async Task StopAsync(string serverId)
    {
        GetDefinition(serverId);

        if (clients.TryRemove(serverId, out var client))
        {
            await client.DisposeAsync();
        }
    }

    public async Task<JsonElement> ListToolsAsync(string serverId, CancellationToken cancellationToken)
    {
        return await GetOrCreateClient(serverId).ListToolsAsync(cancellationToken);
    }

    public async Task<JsonElement> CallToolAsync(
        string serverId,
        string toolName,
        JsonElement? arguments,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            throw new ArgumentException("Informe o nome da tool.", nameof(toolName));
        }

        return await GetOrCreateClient(serverId).CallToolAsync(
            toolName.Trim(),
            arguments,
            cancellationToken);
    }

    public async Task<JsonElement> SendRawRequestAsync(
        string serverId,
        string method,
        JsonElement? parameters,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(method))
        {
            throw new ArgumentException("Informe o metodo MCP.", nameof(method));
        }

        return await GetOrCreateClient(serverId).SendRawRequestAsync(
            method.Trim(),
            parameters,
            cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var client in clients.Values)
        {
            await client.DisposeAsync();
        }

        clients.Clear();
    }

    private McpStdioClient GetOrCreateClient(string serverId)
    {
        var definition = GetDefinition(serverId);
        return clients.GetOrAdd(serverId, _ =>
        {
            var timeoutSeconds = definition.RequestTimeoutSeconds ?? defaultRequestTimeoutSeconds;
            return new McpStdioClient(
                definition,
                paths,
                loggerFactory.CreateLogger<McpStdioClient>(),
                Math.Max(1, timeoutSeconds));
        });
    }

    private McpServerDefinition GetDefinition(string serverId)
    {
        if (definitions.TryGetValue(serverId, out var definition))
        {
            return definition;
        }

        throw new KeyNotFoundException($"Servidor MCP '{serverId}' nao esta configurado.");
    }

    private McpServerInfo ToInfo(McpServerDefinition definition)
    {
        var running = clients.TryGetValue(definition.Id, out var client) && client.IsRunning;

        return new McpServerInfo(
            definition.Id,
            string.IsNullOrWhiteSpace(definition.Name) ? definition.Id : definition.Name,
            paths.ResolveText(definition.Command),
            definition.Args.Select(paths.ResolveText).ToArray(),
            paths.ResolvePath(definition.WorkingDirectory),
            running);
    }
}
