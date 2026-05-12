# syntax=docker/dockerfile:1.7
# FamilyNido backend — .NET 10 ASP.NET Core Minimal API.
# Multi-stage: restore+publish on the SDK, run on the ASP.NET runtime.

ARG DOTNET_VERSION=10.0

# ─── Build stage ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION}-alpine AS build
WORKDIR /src

# Copy just the csproj/solution files first to cache the restore layer.
COPY Directory.Build.props global.json ./
COPY src/FamilyNido.Api/FamilyNido.Api.csproj src/FamilyNido.Api/
COPY src/FamilyNido.Domain/FamilyNido.Domain.csproj src/FamilyNido.Domain/
COPY src/FamilyNido.Persistence/FamilyNido.Persistence.csproj src/FamilyNido.Persistence/
COPY tests/FamilyNido.Tests/FamilyNido.Tests.csproj tests/FamilyNido.Tests/
RUN dotnet restore src/FamilyNido.Api/FamilyNido.Api.csproj

# Now copy the sources and publish a self-contained trim-free release.
COPY . .
RUN dotnet publish src/FamilyNido.Api/FamilyNido.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

# ─── Runtime stage ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-alpine AS runtime
WORKDIR /app

# Non-root user to follow least-privilege. Pre-create the writable
# directories with the right owner so that a Docker volume mounted on them
# at runtime inherits the permissions on first creation.
RUN addgroup -S familynido && adduser -S -G familynido familynido \
    && mkdir -p /app/data/files /home/familynido/.aspnet/DataProtection-Keys \
    && chown -R familynido:familynido /app /home/familynido

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# ICU + tzdata so DateTimeOffset.ToLocalTime() and es-ES formatting work.
# `su-exec` lets the entrypoint drop privileges after the chown step —
# alpine ships it as a tiny ~10 KB binary equivalent to gosu.
RUN apk add --no-cache icu-libs icu-data-full tzdata su-exec

COPY --from=build --chown=familynido:familynido /app/publish .
COPY --chown=root:root deploy/api-entrypoint.sh /usr/local/bin/api-entrypoint.sh
# sed strips any CRLF line endings introduced by Windows checkouts so the
# shebang and statements parse correctly on the Alpine /bin/sh.
RUN sed -i 's/\r$//' /usr/local/bin/api-entrypoint.sh \
    && chmod +x /usr/local/bin/api-entrypoint.sh

# Note: do NOT set USER familynido here. The entrypoint starts as root,
# normalizes ownership of the volume mount points, and then exec-s into the
# .NET host as `familynido` via su-exec.
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
    CMD wget -qO- http://localhost:8080/health/live || exit 1

ENTRYPOINT ["/usr/local/bin/api-entrypoint.sh"]
CMD ["dotnet", "FamilyNido.Api.dll"]
