# syntax=docker/dockerfile:1
# Multi-stage build for GauntletAI.AgentForge.Api
# Railway auto-detects this Dockerfile at the repo root and uses it for the service build.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy central build/package management + solution first for better layer caching.
COPY Directory.Build.props Directory.Packages.props GauntletAI.AgentForge.slnx ./
COPY src/ src/

RUN dotnet restore src/GauntletAI.AgentForge.Api/GauntletAI.AgentForge.Api.csproj
RUN dotnet publish src/GauntletAI.AgentForge.Api/GauntletAI.AgentForge.Api.csproj \
    -c Release \
    --no-restore \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0

# Railway injects PORT at runtime; bind Kestrel to it (default 8080 for local docker run).
ENTRYPOINT ["/bin/sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} exec dotnet GauntletAI.AgentForge.Api.dll"]
