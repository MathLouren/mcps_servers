# Local MCP Tester

Servidor local em C#/.NET 8 para testar MCPs via navegador ou API HTTP.

Ele inicia servidores MCP configurados em `appsettings.json`, conversa com eles por stdio usando JSON-RPC e permite:

- iniciar/parar um MCP local;
- listar tools com `tools/list`;
- executar tools com `tools/call`;
- enviar uma chamada MCP raw;
- ver logs de stdout/stderr do processo MCP.

## Executar

Da raiz do workspace:

```powershell
dotnet build .\mcps_servers.sln -m:1
```

Em seguida:

```powershell
dotnet run --project .\LocalMcpTester\LocalMcpTester.csproj
```

Abra:

```text
http://localhost:5129
```

O perfil HTTP do projeto usa a porta `5129` em `Properties/launchSettings.json`.

## Configurar MCPs

Edite `LocalMcpTester/appsettings.json`:

```json
{
  "McpTester": {
    "DefaultRequestTimeoutSeconds": 30,
    "Servers": [
      {
        "Id": "eprodutos-agents",
        "Name": "Eprodutos Agents",
        "Command": "dotnet",
        "Args": [
          "run",
          "--project",
          "${WorkspaceRoot}\\EprodutosAgents\\EprodutosAgents.csproj"
        ],
        "WorkingDirectory": "${WorkspaceRoot}",
        "RequestTimeoutSeconds": 45,
        "Environment": {
          "ASPNETCORE_ENVIRONMENT": "Production"
        }
      }
    ]
  }
}
```

Placeholders aceitos:

- `${WorkspaceRoot}`: pasta que contem `mcps_servers.sln`.
- `${ContentRoot}`: pasta do projeto `LocalMcpTester`.

Para adicionar outro MCP, acrescente outro item em `McpTester:Servers` com `Id`, `Command`, `Args` e `WorkingDirectory`.

## Uso pelo navegador

1. Selecione o servidor na lateral.
2. Clique em `Iniciar`.
3. Clique em `Listar tools`.
4. Selecione a tool.
5. Preencha `Argumentos JSON`.
6. Clique em `Executar tool`.

Exemplo para o MCP `EprodutosAgents`:

```json
{
  "pn": "7DF4A00UBR"
}
```

## Uso pela API HTTP

Listar servidores configurados:

```powershell
Invoke-RestMethod http://localhost:5129/api/servers
```

Iniciar um MCP:

```powershell
Invoke-RestMethod -Method Post http://localhost:5129/api/servers/eprodutos-agents/start
```

Listar tools:

```powershell
Invoke-RestMethod http://localhost:5129/api/servers/eprodutos-agents/tools
```

Executar uma tool:

```powershell
$body = @{
  arguments = @{
    pn = "7DF4A00UBR"
  }
} | ConvertTo-Json -Depth 5

Invoke-RestMethod `
  -Method Post `
  -ContentType "application/json" `
  -Body $body `
  http://localhost:5129/api/servers/eprodutos-agents/tools/eprodutos_get_product_inventory
```

Enviar chamada MCP raw:

```powershell
$body = @{
  method = "tools/list"
  params = @{}
} | ConvertTo-Json -Depth 5

Invoke-RestMethod `
  -Method Post `
  -ContentType "application/json" `
  -Body $body `
  http://localhost:5129/api/servers/eprodutos-agents/raw
```

Ver logs:

```powershell
Invoke-RestMethod http://localhost:5129/api/servers/eprodutos-agents/logs
```

Parar um MCP:

```powershell
Invoke-RestMethod -Method Post http://localhost:5129/api/servers/eprodutos-agents/stop
```

## Observacoes

- O testador inicia o MCP sob demanda ao listar tools ou executar tools.
- Se o MCP usa banco local, como o `EprodutosAgents`, mantenha o MongoDB rodando em `localhost:27017`.
- Os logs do MCP devem ir para stderr. Saidas nao JSON em stdout sao registradas como log, mas podem atrapalhar clientes MCP reais.
