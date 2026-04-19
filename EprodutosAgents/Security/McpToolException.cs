namespace EprodutosAgents.Security;

public static class McpToolException
{
    public const string GenericMessage = "Falha interna ao executar a consulta MCP.";

    public static bool CanExpose(Exception exception)
    {
        return exception is ArgumentException or UnauthorizedAccessException;
    }
}
