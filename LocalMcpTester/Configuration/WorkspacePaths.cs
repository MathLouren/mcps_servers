using Microsoft.Extensions.Hosting;

namespace LocalMcpTester.Configuration;

public sealed class WorkspacePaths
{
    private readonly IHostEnvironment environment;

    public WorkspacePaths(IHostEnvironment environment)
    {
        this.environment = environment;
        WorkspaceRoot = DiscoverWorkspaceRoot(environment.ContentRootPath)
            ?? DiscoverWorkspaceRoot(AppContext.BaseDirectory)
            ?? Directory.GetCurrentDirectory();
    }

    public string WorkspaceRoot { get; }

    public string ContentRoot => environment.ContentRootPath;

    public string ResolveText(string value)
    {
        return value
            .Replace("${WorkspaceRoot}", WorkspaceRoot, StringComparison.OrdinalIgnoreCase)
            .Replace("${ContentRoot}", ContentRoot, StringComparison.OrdinalIgnoreCase);
    }

    public string ResolvePath(string? value)
    {
        var resolved = ResolveText(string.IsNullOrWhiteSpace(value) ? WorkspaceRoot : value.Trim());
        return Path.IsPathRooted(resolved)
            ? Path.GetFullPath(resolved)
            : Path.GetFullPath(resolved, WorkspaceRoot);
    }

    private static string? DiscoverWorkspaceRoot(string startPath)
    {
        var directory = Directory.Exists(startPath)
            ? new DirectoryInfo(startPath)
            : new FileInfo(startPath).Directory;

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "mcps_servers.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
