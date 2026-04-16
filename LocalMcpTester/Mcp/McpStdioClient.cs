using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using LocalMcpTester.Configuration;

namespace LocalMcpTester.Mcp;

public sealed class McpStdioClient : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly McpServerDefinition definition;
    private readonly WorkspacePaths paths;
    private readonly ILogger<McpStdioClient> logger;
    private readonly SemaphoreSlim lifecycleLock = new(1, 1);
    private readonly SemaphoreSlim writeLock = new(1, 1);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonElement>> pendingRequests = new();
    private readonly ConcurrentQueue<McpLogEntry> logs = new();
    private readonly int requestTimeoutSeconds;
    private int nextRequestId;
    private Process? process;
    private bool initialized;

    public McpStdioClient(
        McpServerDefinition definition,
        WorkspacePaths paths,
        ILogger<McpStdioClient> logger,
        int requestTimeoutSeconds)
    {
        this.definition = definition;
        this.paths = paths;
        this.logger = logger;
        this.requestTimeoutSeconds = requestTimeoutSeconds;
    }

    public bool IsRunning => process is { HasExited: false };

    public IReadOnlyList<McpLogEntry> GetLogs()
    {
        return logs.ToArray();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            if (initialized && IsRunning)
            {
                return;
            }

            await StopCoreAsync();
            StartProcess();

            await SendRequestAsync("initialize", new
            {
                protocolVersion = "2024-11-05",
                capabilities = new { },
                clientInfo = new
                {
                    name = "local-mcp-tester",
                    version = "1.0.0"
                }
            }, cancellationToken);

            await SendNotificationAsync("notifications/initialized", null, cancellationToken);
            initialized = true;
        }
        finally
        {
            lifecycleLock.Release();
        }
    }

    public async Task<JsonElement> ListToolsAsync(CancellationToken cancellationToken)
    {
        await EnsureStartedAsync(cancellationToken);
        return await SendRequestAsync("tools/list", new { }, cancellationToken);
    }

    public async Task<JsonElement> CallToolAsync(
        string toolName,
        JsonElement? arguments,
        CancellationToken cancellationToken)
    {
        await EnsureStartedAsync(cancellationToken);

        var parameters = new JsonObject
        {
            ["name"] = toolName,
            ["arguments"] = arguments.HasValue
                ? JsonNode.Parse(arguments.Value.GetRawText())
                : new JsonObject()
        };

        return await SendRequestAsync("tools/call", parameters, cancellationToken);
    }

    public async Task<JsonElement> SendRawRequestAsync(
        string method,
        JsonElement? parameters,
        CancellationToken cancellationToken)
    {
        await EnsureStartedAsync(cancellationToken);
        return await SendRequestAsync(method, parameters, cancellationToken);
    }

    public async Task StopAsync()
    {
        await lifecycleLock.WaitAsync();
        try
        {
            await StopCoreAsync();
        }
        finally
        {
            lifecycleLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        lifecycleLock.Dispose();
        writeLock.Dispose();
    }

    private async Task EnsureStartedAsync(CancellationToken cancellationToken)
    {
        if (initialized && IsRunning)
        {
            return;
        }

        await StartAsync(cancellationToken);
    }

    private void StartProcess()
    {
        var workingDirectory = paths.ResolvePath(definition.WorkingDirectory);
        var command = paths.ResolveText(definition.Command);
        var startInfo = new ProcessStartInfo
        {
            FileName = command,
            WorkingDirectory = workingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in definition.Args)
        {
            startInfo.ArgumentList.Add(paths.ResolveText(argument));
        }

        foreach (var (key, value) in definition.Environment)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                startInfo.Environment[key] = value is null ? string.Empty : paths.ResolveText(value);
            }
        }

        AddLog("tester", $"Starting: {command} {string.Join(' ', startInfo.ArgumentList)}");
        AddLog("tester", $"Working directory: {workingDirectory}");

        process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Nao foi possivel iniciar o servidor MCP '{definition.Id}'.");

        _ = Task.Run(ReadStdoutLoopAsync);
        _ = Task.Run(ReadStderrLoopAsync);
    }

    private async Task<JsonElement> SendRequestAsync(
        string method,
        object? parameters,
        CancellationToken cancellationToken)
    {
        var currentProcess = EnsureProcessIsAlive();
        var requestId = Interlocked.Increment(ref nextRequestId);
        var requestIdKey = requestId.ToString();
        var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!pendingRequests.TryAdd(requestIdKey, completion))
        {
            throw new InvalidOperationException($"Id JSON-RPC duplicado: {requestIdKey}");
        }

        var envelope = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = requestId,
            ["method"] = method
        };

        var parameterNode = ToJsonNode(parameters);
        if (parameterNode is not null)
        {
            envelope["params"] = parameterNode;
        }

        await WriteJsonLineAsync(currentProcess, envelope, cancellationToken);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, requestTimeoutSeconds)));

        try
        {
            return await completion.Task.WaitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            pendingRequests.TryRemove(requestIdKey, out _);
            throw new TimeoutException(
                $"Timeout aguardando resposta do metodo MCP '{method}' no servidor '{definition.Id}'.");
        }
    }

    private async Task SendNotificationAsync(
        string method,
        object? parameters,
        CancellationToken cancellationToken)
    {
        var currentProcess = EnsureProcessIsAlive();
        var envelope = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["method"] = method
        };

        var parameterNode = ToJsonNode(parameters);
        if (parameterNode is not null)
        {
            envelope["params"] = parameterNode;
        }

        await WriteJsonLineAsync(currentProcess, envelope, cancellationToken);
    }

    private async Task WriteJsonLineAsync(
        Process currentProcess,
        JsonObject envelope,
        CancellationToken cancellationToken)
    {
        var json = envelope.ToJsonString(JsonOptions);

        await writeLock.WaitAsync(cancellationToken);
        try
        {
            if (currentProcess.HasExited)
            {
                throw new InvalidOperationException(
                    $"O processo MCP '{definition.Id}' encerrou com codigo {currentProcess.ExitCode}.");
            }

            await currentProcess.StandardInput.WriteLineAsync(json);
            await currentProcess.StandardInput.FlushAsync();
        }
        finally
        {
            writeLock.Release();
        }
    }

    private Process EnsureProcessIsAlive()
    {
        if (process is null)
        {
            throw new InvalidOperationException($"O servidor MCP '{definition.Id}' nao foi iniciado.");
        }

        if (process.HasExited)
        {
            throw new InvalidOperationException(
                $"O processo MCP '{definition.Id}' encerrou com codigo {process.ExitCode}.");
        }

        return process;
    }

    private async Task ReadStdoutLoopAsync()
    {
        var currentProcess = process;
        if (currentProcess is null)
        {
            return;
        }

        try
        {
            while (await currentProcess.StandardOutput.ReadLineAsync() is { } line)
            {
                HandleStdoutLine(line);
            }
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "Erro lendo stdout do MCP {McpServerId}", definition.Id);
            AddLog("tester", $"Erro lendo stdout: {exception.Message}");
        }
        finally
        {
            CompletePendingRequests(new InvalidOperationException(
                $"Stdout do servidor MCP '{definition.Id}' foi encerrado."));
        }
    }

    private async Task ReadStderrLoopAsync()
    {
        var currentProcess = process;
        if (currentProcess is null)
        {
            return;
        }

        try
        {
            while (await currentProcess.StandardError.ReadLineAsync() is { } line)
            {
                AddLog("stderr", line);
            }
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "Erro lendo stderr do MCP {McpServerId}", definition.Id);
            AddLog("tester", $"Erro lendo stderr: {exception.Message}");
        }
    }

    private void HandleStdoutLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        using var document = TryParseJson(line);
        if (document is null)
        {
            AddLog("stdout", line);
            return;
        }

        var root = document.RootElement;
        if (!root.TryGetProperty("id", out var idElement) ||
            idElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            AddLog("stdout", line);
            return;
        }

        var id = ToIdKey(idElement);
        if (id is null || !pendingRequests.TryRemove(id, out var completion))
        {
            AddLog("stdout", line);
            return;
        }

        if (root.TryGetProperty("error", out var error))
        {
            var errorClone = error.Clone();
            completion.TrySetException(new McpRpcException(
                $"Erro JSON-RPC retornado pelo servidor MCP '{definition.Id}'.",
                errorClone));
            return;
        }

        if (root.TryGetProperty("result", out var result))
        {
            completion.TrySetResult(result.Clone());
            return;
        }

        completion.TrySetResult(JsonDocument.Parse("{}").RootElement.Clone());
    }

    private async Task StopCoreAsync()
    {
        initialized = false;

        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                try
                {
                    process.StandardInput.Close();
                }
                catch (InvalidOperationException)
                {
                }

                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                try
                {
                    await process.WaitForExitAsync(timeout.Token);
                }
                catch (OperationCanceledException)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync();
                }
            }
        }
        finally
        {
            process.Dispose();
            process = null;
            CompletePendingRequests(new InvalidOperationException(
                $"Servidor MCP '{definition.Id}' foi parado."));
        }
    }

    private void CompletePendingRequests(Exception exception)
    {
        foreach (var request in pendingRequests)
        {
            if (pendingRequests.TryRemove(request.Key, out var completion))
            {
                completion.TrySetException(exception);
            }
        }
    }

    private void AddLog(string stream, string message)
    {
        logs.Enqueue(new McpLogEntry(DateTimeOffset.Now, stream, message));
        while (logs.Count > 300 && logs.TryDequeue(out _))
        {
        }
    }

    private static JsonNode? ToJsonNode(object? value)
    {
        return value switch
        {
            null => null,
            JsonNode node => node.DeepClone(),
            JsonElement element => JsonNode.Parse(element.GetRawText()),
            _ => JsonSerializer.SerializeToNode(value, JsonOptions)
        };
    }

    private static JsonDocument? TryParseJson(string line)
    {
        try
        {
            return JsonDocument.Parse(line);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ToIdKey(JsonElement idElement)
    {
        return idElement.ValueKind switch
        {
            JsonValueKind.Number when idElement.TryGetInt64(out var number) => number.ToString(),
            JsonValueKind.String => idElement.GetString(),
            _ => idElement.GetRawText()
        };
    }
}
