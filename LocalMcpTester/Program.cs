using System.Text.Json;
using LocalMcpTester.Configuration;
using LocalMcpTester.Mcp;

var projectRoot = ResolveProjectRoot();
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = projectRoot,
    WebRootPath = Path.Combine(projectRoot, "wwwroot")
});

builder.Services.ConfigureHttpJsonOptions(static options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.WriteIndented = true;
});

builder.Services.Configure<McpTesterOptions>(
    builder.Configuration.GetSection(McpTesterOptions.SectionName));
builder.Services.AddSingleton<WorkspacePaths>();
builder.Services.AddSingleton<McpServerManager>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

var api = app.MapGroup("/api");

api.MapGet("/servers", (McpServerManager manager) => manager.ListServers());

api.MapPost("/servers/{serverId}/start", async (
    string serverId,
    McpServerManager manager,
    CancellationToken cancellationToken) =>
{
    try
    {
        await manager.StartAsync(serverId, cancellationToken);
        return Results.Ok(manager.GetServer(serverId));
    }
    catch (Exception exception)
    {
        return ToProblem(exception);
    }
});

api.MapPost("/servers/{serverId}/stop", async (
    string serverId,
    McpServerManager manager) =>
{
    try
    {
        await manager.StopAsync(serverId);
        return Results.Ok(manager.GetServer(serverId));
    }
    catch (Exception exception)
    {
        return ToProblem(exception);
    }
});

api.MapGet("/servers/{serverId}/logs", (string serverId, McpServerManager manager) =>
{
    try
    {
        return Results.Ok(manager.GetLogs(serverId));
    }
    catch (Exception exception)
    {
        return ToProblem(exception);
    }
});

api.MapGet("/servers/{serverId}/tools", async (
    string serverId,
    McpServerManager manager,
    CancellationToken cancellationToken) =>
{
    try
    {
        var result = await manager.ListToolsAsync(serverId, cancellationToken);
        return Results.Json(result);
    }
    catch (Exception exception)
    {
        return ToProblem(exception);
    }
});

api.MapPost("/servers/{serverId}/tools/{toolName}", async (
    string serverId,
    string toolName,
    HttpRequest request,
    McpServerManager manager,
    CancellationToken cancellationToken) =>
{
    try
    {
        var arguments = await ReadToolArgumentsAsync(request, cancellationToken);
        var result = await manager.CallToolAsync(serverId, toolName, arguments, cancellationToken);
        return Results.Json(result);
    }
    catch (Exception exception)
    {
        return ToProblem(exception);
    }
});

api.MapPost("/servers/{serverId}/raw", async (
    string serverId,
    RawMcpRequest request,
    McpServerManager manager,
    CancellationToken cancellationToken) =>
{
    try
    {
        if (string.IsNullOrWhiteSpace(request.Method))
        {
            return Results.BadRequest(new { error = "Informe o metodo MCP." });
        }

        var result = await manager.SendRawRequestAsync(
            serverId,
            request.Method,
            request.Params,
            cancellationToken);

        return Results.Json(result);
    }
    catch (Exception exception)
    {
        return ToProblem(exception);
    }
});

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

static async Task<JsonElement?> ReadToolArgumentsAsync(HttpRequest request, CancellationToken cancellationToken)
{
    if (request.ContentLength == 0)
    {
        return null;
    }

    using var document = await JsonDocument.ParseAsync(request.Body, cancellationToken: cancellationToken);
    var root = document.RootElement;

    if (root.ValueKind == JsonValueKind.Object &&
        root.TryGetProperty("arguments", out var wrappedArguments))
    {
        root = wrappedArguments;
    }

    if (root.ValueKind != JsonValueKind.Object)
    {
        throw new ArgumentException("Os argumentos da tool devem ser um objeto JSON.");
    }

    return root.Clone();
}

static IResult ToProblem(Exception exception)
{
    return exception switch
    {
        KeyNotFoundException => Results.NotFound(new { error = exception.Message }),
        ArgumentException => Results.BadRequest(new { error = exception.Message }),
        McpRpcException rpcException => Results.Json(
            new { error = rpcException.Message, rpc = rpcException.Error },
            statusCode: StatusCodes.Status502BadGateway),
        _ => Results.Json(
            new { error = exception.Message },
            statusCode: StatusCodes.Status500InternalServerError)
    };
}

static string ResolveProjectRoot()
{
    var currentDirectory = Directory.GetCurrentDirectory();
    var currentDirectoryProject = Path.Combine(currentDirectory, "LocalMcpTester.csproj");
    if (File.Exists(currentDirectoryProject))
    {
        return currentDirectory;
    }

    var childProject = Path.Combine(currentDirectory, "LocalMcpTester", "LocalMcpTester.csproj");
    if (File.Exists(childProject))
    {
        return Path.Combine(currentDirectory, "LocalMcpTester");
    }

    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "LocalMcpTester.csproj")))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    return AppContext.BaseDirectory;
}

public sealed record RawMcpRequest(string Method, JsonElement? Params);
