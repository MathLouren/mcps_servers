# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY EprodutosAgents/EprodutosAgents.csproj EprodutosAgents/
RUN dotnet restore EprodutosAgents/EprodutosAgents.csproj

COPY EprodutosAgents/ EprodutosAgents/
RUN dotnet publish EprodutosAgents/EprodutosAgents.csproj \
    --configuration Release \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:8.0-bookworm-slim AS runtime
WORKDIR /app

ENV DOTNET_ENVIRONMENT=Production

COPY --from=build /app/publish .

USER $APP_UID

ENTRYPOINT ["dotnet", "EprodutosAgents.dll"]
