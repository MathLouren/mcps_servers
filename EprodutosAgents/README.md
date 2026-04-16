# Eprodutos Agents

Servidor MCP 100% em C#/.NET 8 para consultar as collections `products` e `stocks` do MongoDB local.

## MongoDB

O MongoDB usa a namespace `database.collection`. Para os exemplos informados, este projeto conecta no banco `eprodutos` e nas collections:

- `products`
- `stocks`

A connection string configurada por padrao e:

```text
mongodb://localhost:27017/eprodutos
```

Se for fornecida uma URI com collection no caminho, como `mongodb://localhost:27017/eprodutos/eprodutos/stocks`, o servidor normaliza a conexao para o banco `eprodutos` e usa os nomes das collections da configuracao.

## Configuracao

Edite `appsettings.json` ou use variaveis de ambiente:

```powershell
$env:EPRODUTOS_ACCESS_PROFILE = "customer"
$env:EPRODUTOS_MONGO_CONNECTION_STRING = "mongodb://localhost:27017/eprodutos"
$env:EPRODUTOS_MONGO_DATABASE = "eprodutos"
$env:EPRODUTOS_PRODUCTS_COLLECTION = "products"
$env:EPRODUTOS_STOCKS_COLLECTION = "stocks"
```

`EPRODUTOS_ACCESS_PROFILE` aceita:

- `customer`: acesso limitado. Nao retorna `price`, `process`, `raw_excel_id`, `backlog` ou `backlog_forecast`.
- `employee`: acesso completo aos campos de estoque.

O padrao do servidor e `customer`, para evitar exposicao acidental de campos internos.

## Executar

Da raiz `C:\Users\Matheus\Desktop\mcps_servers`:

```powershell
dotnet build .\mcps_servers.sln
dotnet run --project .\EprodutosAgents\EprodutosAgents.csproj
```

Para executar como funcionario:

```powershell
$env:EPRODUTOS_ACCESS_PROFILE = "employee"
dotnet run --project .\EprodutosAgents\EprodutosAgents.csproj
```

Para executar como cliente:

```powershell
$env:EPRODUTOS_ACCESS_PROFILE = "customer"
dotnet run --project .\EprodutosAgents\EprodutosAgents.csproj
```

Ou dentro da pasta do projeto:

```powershell
dotnet restore .\EprodutosAgents.csproj
dotnet build .\EprodutosAgents.csproj
dotnet run --project .\EprodutosAgents.csproj
```

## Producao com Docker

O servidor MCP usa transporte `stdio`. Em producao, o cliente MCP deve iniciar o container com stdin aberto. Use `docker compose run --rm -i -T ...`; nao use `docker compose up -d` para conectar MCP por `stdio`.

### 1. Preparar variaveis

Na VM Linux, dentro da pasta do projeto:

```bash
cp EprodutosAgents/eprodutos.env.example EprodutosAgents/eprodutos.env
nano EprodutosAgents/eprodutos.env
```

Para MongoDB instalado diretamente na VM host, mantenha:

```env
EPRODUTOS_MONGO_CONNECTION_STRING=mongodb://host.docker.internal:27017/eprodutos
```

O `docker-compose.yml` ja inclui `host.docker.internal:host-gateway` para Linux.

### 2. Build da imagem

```bash
docker compose build
```

### 3. Testar manualmente

Cliente:

```bash
docker compose run --rm -i -T eprodutos-customer
```

Funcionario:

```bash
docker compose run --rm -i -T eprodutos-employee
```

O processo pode ficar sem imprimir nada, aguardando mensagens MCP via `stdio`.

### 4. Configurar no cliente MCP

Exemplo usando o projeto em `/opt/eprodutos-mcp` na VM:

```json
{
  "mcpServers": {
    "eprodutos-customer": {
      "command": "docker",
      "args": [
        "compose",
        "-f",
        "/opt/eprodutos-mcp/docker-compose.yml",
        "run",
        "--rm",
        "-i",
        "-T",
        "eprodutos-customer"
      ]
    },
    "eprodutos-employee": {
      "command": "docker",
      "args": [
        "compose",
        "-f",
        "/opt/eprodutos-mcp/docker-compose.yml",
        "run",
        "--rm",
        "-i",
        "-T",
        "eprodutos-employee"
      ]
    }
  }
}
```

Em ambiente de cliente, configure apenas `eprodutos-customer`. Nao exponha `eprodutos-employee` para usuarios finais.

### Docker run direto

Tambem e possivel rodar sem Compose:

```bash
docker build -f EprodutosAgents/Dockerfile -t eprodutos-agents:latest .
docker run --rm -i --env-file EprodutosAgents/eprodutos.env -e EPRODUTOS_ACCESS_PROFILE=customer eprodutos-agents:latest
docker run --rm -i --env-file EprodutosAgents/eprodutos.env -e EPRODUTOS_ACCESS_PROFILE=employee eprodutos-agents:latest
```

## Tools MCP

- `eprodutos_search_products`: busca em `products` por PN, categoria e fabricante.
- `eprodutos_get_product_by_pn`: retorna um produto pelo PN exato.
- `eprodutos_search_stocks`: busca em `stocks` por PN, descricao, UF, CD, status e validade. No perfil `employee`, tambem busca e retorna `process`.
- `eprodutos_get_stocks_by_pn`: retorna os estoques mais recentes de um PN.
- `eprodutos_summarize_stock_by_pn`: resume quantidade, preco de venda e validade por PN.
- `eprodutos_get_product_inventory`: relaciona `products.pn` com `stocks.pn`.

## Exemplo de cliente MCP com dois perfis

```json
{
  "mcpServers": {
    "eprodutos-employee": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "C:\\Users\\Matheus\\Desktop\\mcps_servers\\EprodutosAgents\\EprodutosAgents.csproj"
      ],
      "env": {
        "EPRODUTOS_ACCESS_PROFILE": "employee"
      }
    },
    "eprodutos-customer": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "C:\\Users\\Matheus\\Desktop\\mcps_servers\\EprodutosAgents\\EprodutosAgents.csproj"
      ],
      "env": {
        "EPRODUTOS_ACCESS_PROFILE": "customer"
      }
    }
  }
}
```

Use apenas o servidor do perfil correto em cada cliente MCP. Nao exponha o servidor `employee` para usuarios finais.
