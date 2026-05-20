# Multi-stage build for OpsPilotAI
# Stage 1: restore & publish (SDK image — large, not shipped)
# Stage 2: runtime (ASP.NET runtime image — minimal, what gets deployed)

# ── Build stage ────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project file first and restore separately so NuGet layers are cached
# even when only source files change.
COPY OpsPilotAI.csproj ./
RUN dotnet restore OpsPilotAI.csproj --locked-mode

# Copy source and publish a trimmed, release build
COPY . .
RUN dotnet publish OpsPilotAI.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

# ── Runtime stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Run as non-root user for security
RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser
USER appuser

COPY --from=build /app/publish .

# Kestrel default port — overridden by ASPNETCORE_URLS or docker-compose ports mapping
EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "OpsPilotAI.dll"]
