# Eprodutos MCP

Servidor MCP em C#/.NET 8, separado do `eProdutosServer`, usando transporte HTTP autenticado por API key.

## MongoDB

Padrao local:

```text
mongodb://localhost:27017/
```

Banco padrao: `eprodutos`.

Collections usadas:

- `products`
- `stocks`
- `users`
- `mcp_api_keys`
- `mcp_audit_logs`

O servidor le os usuarios existentes em `users`. As collections `mcp_api_keys` e `mcp_audit_logs` sao criadas/usadas pelo MCP.

## Configuracao

Variaveis principais:

```powershell
$env:EPRODUTOS_MONGO_CONNECTION_STRING = "mongodb://localhost:27017/"
$env:EPRODUTOS_MONGO_DATABASE = "eprodutos"
$env:EPRODUTOS_MCP_ADMIN_KEY = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$env:ASPNETCORE_URLS = "http://0.0.0.0:8080"
```

Dentro de container Docker, se o MongoDB estiver no host, use:

```env
EPRODUTOS_MONGO_CONNECTION_STRING=mongodb://host.docker.internal:27017/
```

## Executar Local

Da raiz `C:\Users\Matheus\Desktop\mcps_servers`:

```powershell
dotnet build .\mcps_servers.sln
$env:EPRODUTOS_MCP_ADMIN_KEY = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
dotnet run --project .\EprodutosAgents\EprodutosAgents.csproj
```

Endpoint MCP:

```text
http://localhost:8080/mcp
```

Health check:

```text
http://localhost:8080/health
```

## Criar API Key

Crie uma API key para um usuario existente na collection `users`.

Por email:

```powershell
$body = @{
  email = "usuario@empresa.com"
  name = "MCP principal"
  created_by = "admin"
} | ConvertTo-Json

Invoke-RestMethod `
  -Method Post `
  -Uri "http://localhost:8080/api/mcp/api-keys" `
  -Headers @{ "X-MCP-ADMIN-KEY" = $env:EPRODUTOS_MCP_ADMIN_KEY } `
  -ContentType "application/json" `
  -Body $body
```

Resposta:

```json
{
  "api_key": "eprod_mcp_mk_xxx.yyy",
  "key_id": "mk_xxx",
  "user_id": "...",
  "email": "usuario@empresa.com",
  "role": "customer",
  "expires_at": "2026-07-17T12:00:00Z"
}
```

Guarde `api_key` na hora. O Mongo salva apenas hash, salt e metadados.

Listar chaves:

```powershell
Invoke-RestMethod `
  -Uri "http://localhost:8080/api/mcp/api-keys?include_revoked=false" `
  -Headers @{ "X-MCP-ADMIN-KEY" = $env:EPRODUTOS_MCP_ADMIN_KEY }
```

Revogar:

```powershell
Invoke-RestMethod `
  -Method Delete `
  -Uri "http://localhost:8080/api/mcp/api-keys/mk_xxx" `
  -Headers @{ "X-MCP-ADMIN-KEY" = $env:EPRODUTOS_MCP_ADMIN_KEY }
```

Por padrao, ao criar uma nova chave para o mesmo usuario, chaves ativas anteriores sao revogadas. Novas chaves expiram em 90 dias; opcionalmente envie `expires_at`, limitado a 365 dias.

## Autenticacao MCP

Cada requisicao MCP deve enviar a API key do usuario:

```http
Authorization: Bearer eprod_mcp_mk_xxx.yyy
```

Tambem funciona:

```http
X-Api-Key: eprod_mcp_mk_xxx.yyy
```

O servidor resolve a chave em `mcp_api_keys`, carrega o usuario em `users` e usa `role` para decidir o retorno.

## Formato Das Respostas

As tools MCP retornam texto em tabelas Markdown para facilitar leitura pelo usuario.

Exemplo para funcionario:

```text
| pn | uf | cd | description | process | quantity | price | sale_price | currency | date | status | validity |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 7DF4A00UBR | ES | ES | SERVIDOR TORRE LENOVO ST50 V3 | Tabela Mazer - Lenovo ISG | 5 | 783955 | 940746 | R$ | 2026-01-07T19:28:19.115+00:00 | 0 | 14/01/2026 |
```

Clientes recebem tabelas sem a coluna `price`.

## Regras De Dados

- `admin`, `manager`, `employee`: retornam `price` e `sale_price`; podem filtrar por `price` e `process`.
- `customer`, `distributor`, `manufacturer`: retornam `sale_price`, nunca `price`.
- Tentativas de cliente filtrar por `price` ou `process` sao bloqueadas.
- `allowedProcesses` e `allowedManufacturers` do usuario sao aplicados como escopo adicional em todas as consultas de estoque/produto.
- `products` e a fonte de `pn`, `category` e `manufacturer`.
- `stocks` e a fonte de `uf`, `cd`, `description`, `process`, `quantity`, `price`, `sale_price`, `currency`, `date`, `status` e `validity`.
- As buscas de estoque relacionam `products.pn` com `stocks.pn`; filtros por `category` e `manufacturer` sempre consultam a collection `products`.

Toda chamada de tool grava auditoria em `mcp_audit_logs`, com usuario, role, `apiKeyId`, tool, argumentos, status, duracao e IP.

## Segurança

- Endpoints MCP exigem API key em `Authorization: Bearer ...` ou `X-Api-Key`.
- Endpoints administrativos exigem `EPRODUTOS_MCP_ADMIN_KEY` forte, com no minimo 32 caracteres; valores fracos de exemplo deixam esses endpoints desabilitados.
- Entradas textuais, page/page_size, corpo e headers HTTP possuem limites.
- Respostas em Markdown escapam conteudo vindo do banco e truncam celulas longas.
- Erros internos das tools sao mascarados para o cliente e ficam registrados apenas na auditoria.
- Rate limit por IP: 10 req/min para administracao e 120 req/min para MCP/health.
- O `docker-compose.yml` publica em `127.0.0.1` por padrao. Para expor externamente, use proxy HTTPS e configure `EPRODUTOS_MCP_BIND_ADDRESS` conscientemente.

## Tools MCP

- `eprodutos_search_products`
- `eprodutos_get_product_by_pn`
- `eprodutos_search_stocks`: aceita `category` e `manufacturer` da collection `products`, alem dos campos de `stocks`.
- `eprodutos_search_inventory`: busca inventario cruzando `products` com `stocks` e aceita `search_term`, `uf`, `cd`, `process` e `category`.
- `eprodutos_get_stocks_by_pn`: retorna estoque junto com `category` e `manufacturer` vindos de `products`.
- `eprodutos_summarize_stock_by_pn`
- `eprodutos_get_product_inventory`

Exemplo de busca usando a collection `products`:

```text
eprodutos_search_stocks(pn: "PBE120GS25SSDR", category: "Componente", manufacturer: "PATRIOT")
```

Resposta para funcionario:

```text
| pn | category | manufacturer | uf | cd | description | process | quantity | price | sale_price | currency | date | status | validity |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| PBE120GS25SSDR | Componente | PATRIOT | ES | ES | ... | ... | 5 | 783955 | 940746 | R$ | 2026-01-07T19:28:19.115+00:00 | 0 | 14/01/2026 |
```

Exemplo de busca cruzada de inventario:

```text
eprodutos_search_inventory(search_term: "ST50", uf: "ES", cd: "ES", category: "Servidor")
```

O retorno segue a role autenticada: `admin`, `manager` e `employee` recebem `process`, `price` e `sale_price`; perfis externos recebem somente os campos seguros.

## Docker

Preparar env:

```bash
cp EprodutosAgents/eprodutos.env.example EprodutosAgents/eprodutos.env
```

Build e execucao:

```bash
docker compose build
docker compose up -d
```

URL publicada:

```text
http://localhost:8080/mcp
```

Docker run direto:

```bash
docker build -f EprodutosAgents/Dockerfile -t eprodutos-mcp:latest .
docker run --rm -p 8080:8080 --env-file EprodutosAgents/eprodutos.env eprodutos-mcp:latest
```
