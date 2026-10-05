# =============================================================================
# ParkEase API - Multi-Stage Dockerfile
# Build context : backend/
# Target        : Google Cloud Run
# .NET version  : 9.0 (mcr.microsoft.com/dotnet/sdk:9.0 / aspnet:9.0)
#
# Dependency graph resolved from .csproj ProjectReference entries:
#
#   ParkingApp.API
#     +-- ParkingApp.Application
#     |     +-- ParkingApp.BuildingBlocks
#     +-- ParkingApp.Infrastructure
#     |     +-- ParkingApp.Application
#     |     +-- ParkingApp.BuildingBlocks
#     |     +-- Modules/Messaging/ParkingApp.Messaging.Contracts
#     |     +-- Modules/Messaging/ParkingApp.Messaging.Domain
#     |     +-- Modules/Messaging/ParkingApp.Messaging.Infrastructure
#     |     +-- Modules/Identity/ParkingApp.Identity.Infrastructure
#     |     |     +-- ParkingApp.Identity.Domain
#     |     |     +-- ParkingApp.Identity.Contracts
#     |     |     +-- ParkingApp.Identity.Application
#     |     |     +-- Modules/Corporate/ParkingApp.Corporate.Contracts
#     |     +-- Modules/Identity/ParkingApp.Identity.Domain
#     |     +-- Modules/Identity/ParkingApp.Identity.Contracts
#     |     +-- Modules/Marketplace/ParkingApp.Marketplace.Contracts
#     |     +-- Modules/Marketplace/ParkingApp.Marketplace.Domain
#     |     +-- Modules/Marketplace/ParkingApp.Marketplace.Application
#     |     +-- Modules/Marketplace/ParkingApp.Marketplace.Infrastructure
#     |     +-- Modules/Corporate/ParkingApp.Corporate.Domain
#     |     +-- Modules/Corporate/ParkingApp.Corporate.Infrastructure
#     |     +-- Modules/Corporate/ParkingApp.Corporate.Contracts
#     |     +-- Modules/Admin/ParkingApp.Admin.Domain
#     |     +-- Modules/Admin/ParkingApp.Admin.Infrastructure
#     +-- ParkingApp.BuildingBlocks
#     +-- Modules/Notifications/ParkingApp.Notifications.Infrastructure
#     +-- Modules/Messaging/ParkingApp.Messaging.Application
#     +-- Modules/Identity/ParkingApp.Identity.Application
#     +-- Modules/Identity/ParkingApp.Identity.Contracts
#     +-- Modules/Marketplace/ParkingApp.Marketplace.Application
#     +-- Modules/Marketplace/ParkingApp.Marketplace.Infrastructure
#     +-- Modules/Corporate/ParkingApp.Corporate.Application
#     +-- Modules/Admin/ParkingApp.Admin.Application
#
# NOT included (confirmed no reference from API):
#   ParkingApp.Gateway  (standalone YARP proxy - separate deployment unit)
#   tests/              (unit/integration tests)
#   ResetOutbox/        (maintenance tool)
#   frontend/           (Cloudflare Pages)
#   Mobile/             (separate app)
# =============================================================================

# ---------------------------------------------------------------------------
# Stage 1: base
# Minimal ASP.NET Core 9 runtime image used as the parent for the final image.
# Declared early so Docker can pull it in parallel while the SDK stages run.
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
# Informational only - Cloud Run uses PORT env var, not EXPOSE
EXPOSE 8080

# ---------------------------------------------------------------------------
# Stage 2: build
# Full .NET 9 SDK image. Responsible for restore and source staging only.
#
# Layer-caching strategy:
#   1. Copy ONLY .csproj files
#   2. Run dotnet restore  (cached until any .csproj changes)
#   3. Copy all source files  (layer is invalidated only when source changes)
#
# dotnet publish (with --no-restore) runs in stage 3, which inherits this
# stage, so the restore cache is automatically reused there.
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# -- Copy project files for NuGet restore layer cache -----------------------

# Core projects
COPY ["src/ParkingApp.API/ParkingApp.API.csproj",                   "src/ParkingApp.API/"]
COPY ["src/ParkingApp.Application/ParkingApp.Application.csproj",   "src/ParkingApp.Application/"]
COPY ["src/ParkingApp.Infrastructure/ParkingApp.Infrastructure.csproj", "src/ParkingApp.Infrastructure/"]
COPY ["src/ParkingApp.BuildingBlocks/ParkingApp.BuildingBlocks.csproj", "src/ParkingApp.BuildingBlocks/"]

# Admin module
COPY ["src/Modules/Admin/ParkingApp.Admin.Application/ParkingApp.Admin.Application.csproj",     "src/Modules/Admin/ParkingApp.Admin.Application/"]
COPY ["src/Modules/Admin/ParkingApp.Admin.Contracts/ParkingApp.Admin.Contracts.csproj",         "src/Modules/Admin/ParkingApp.Admin.Contracts/"]
COPY ["src/Modules/Admin/ParkingApp.Admin.Domain/ParkingApp.Admin.Domain.csproj",               "src/Modules/Admin/ParkingApp.Admin.Domain/"]
COPY ["src/Modules/Admin/ParkingApp.Admin.Infrastructure/ParkingApp.Admin.Infrastructure.csproj", "src/Modules/Admin/ParkingApp.Admin.Infrastructure/"]

# Corporate module
COPY ["src/Modules/Corporate/ParkingApp.Corporate.Application/ParkingApp.Corporate.Application.csproj",     "src/Modules/Corporate/ParkingApp.Corporate.Application/"]
COPY ["src/Modules/Corporate/ParkingApp.Corporate.Contracts/ParkingApp.Corporate.Contracts.csproj",         "src/Modules/Corporate/ParkingApp.Corporate.Contracts/"]
COPY ["src/Modules/Corporate/ParkingApp.Corporate.Domain/ParkingApp.Corporate.Domain.csproj",               "src/Modules/Corporate/ParkingApp.Corporate.Domain/"]
COPY ["src/Modules/Corporate/ParkingApp.Corporate.Infrastructure/ParkingApp.Corporate.Infrastructure.csproj", "src/Modules/Corporate/ParkingApp.Corporate.Infrastructure/"]

# Identity module
COPY ["src/Modules/Identity/ParkingApp.Identity.Application/ParkingApp.Identity.Application.csproj",     "src/Modules/Identity/ParkingApp.Identity.Application/"]
COPY ["src/Modules/Identity/ParkingApp.Identity.Contracts/ParkingApp.Identity.Contracts.csproj",         "src/Modules/Identity/ParkingApp.Identity.Contracts/"]
COPY ["src/Modules/Identity/ParkingApp.Identity.Domain/ParkingApp.Identity.Domain.csproj",               "src/Modules/Identity/ParkingApp.Identity.Domain/"]
COPY ["src/Modules/Identity/ParkingApp.Identity.Infrastructure/ParkingApp.Identity.Infrastructure.csproj", "src/Modules/Identity/ParkingApp.Identity.Infrastructure/"]

# Marketplace module
COPY ["src/Modules/Marketplace/ParkingApp.Marketplace.Application/ParkingApp.Marketplace.Application.csproj",       "src/Modules/Marketplace/ParkingApp.Marketplace.Application/"]
COPY ["src/Modules/Marketplace/ParkingApp.Marketplace.Contracts/ParkingApp.Marketplace.Contracts.csproj",           "src/Modules/Marketplace/ParkingApp.Marketplace.Contracts/"]
COPY ["src/Modules/Marketplace/ParkingApp.Marketplace.Domain/ParkingApp.Marketplace.Domain.csproj",                 "src/Modules/Marketplace/ParkingApp.Marketplace.Domain/"]
COPY ["src/Modules/Marketplace/ParkingApp.Marketplace.Infrastructure/ParkingApp.Marketplace.Infrastructure.csproj", "src/Modules/Marketplace/ParkingApp.Marketplace.Infrastructure/"]

# Messaging module
COPY ["src/Modules/Messaging/ParkingApp.Messaging.Application/ParkingApp.Messaging.Application.csproj",       "src/Modules/Messaging/ParkingApp.Messaging.Application/"]
COPY ["src/Modules/Messaging/ParkingApp.Messaging.Contracts/ParkingApp.Messaging.Contracts.csproj",           "src/Modules/Messaging/ParkingApp.Messaging.Contracts/"]
COPY ["src/Modules/Messaging/ParkingApp.Messaging.Domain/ParkingApp.Messaging.Domain.csproj",                 "src/Modules/Messaging/ParkingApp.Messaging.Domain/"]
COPY ["src/Modules/Messaging/ParkingApp.Messaging.Infrastructure/ParkingApp.Messaging.Infrastructure.csproj", "src/Modules/Messaging/ParkingApp.Messaging.Infrastructure/"]

# Notifications module
COPY ["src/Modules/Notifications/ParkingApp.Notifications.Application/ParkingApp.Notifications.Application.csproj",       "src/Modules/Notifications/ParkingApp.Notifications.Application/"]
COPY ["src/Modules/Notifications/ParkingApp.Notifications.Contracts/ParkingApp.Notifications.Contracts.csproj",           "src/Modules/Notifications/ParkingApp.Notifications.Contracts/"]
COPY ["src/Modules/Notifications/ParkingApp.Notifications.Domain/ParkingApp.Notifications.Domain.csproj",                 "src/Modules/Notifications/ParkingApp.Notifications.Domain/"]
COPY ["src/Modules/Notifications/ParkingApp.Notifications.Infrastructure/ParkingApp.Notifications.Infrastructure.csproj", "src/Modules/Notifications/ParkingApp.Notifications.Infrastructure/"]

# Restore NuGet packages. Walking the dependency graph from ParkingApp.API
# automatically includes all transitive project references above.
RUN dotnet restore "src/ParkingApp.API/ParkingApp.API.csproj"

# -- Copy all required source files -----------------------------------------
# This layer is invalidated only when .cs / content files change,
# not when .csproj-only files change (thanks to the ordering above).

COPY ["src/ParkingApp.API/",            "src/ParkingApp.API/"]
COPY ["src/ParkingApp.Application/",    "src/ParkingApp.Application/"]
COPY ["src/ParkingApp.Infrastructure/", "src/ParkingApp.Infrastructure/"]
COPY ["src/ParkingApp.BuildingBlocks/", "src/ParkingApp.BuildingBlocks/"]
COPY ["src/Modules/Admin/",             "src/Modules/Admin/"]
COPY ["src/Modules/Corporate/",         "src/Modules/Corporate/"]
COPY ["src/Modules/Identity/",          "src/Modules/Identity/"]
COPY ["src/Modules/Marketplace/",       "src/Modules/Marketplace/"]
COPY ["src/Modules/Messaging/",         "src/Modules/Messaging/"]
COPY ["src/Modules/Notifications/",     "src/Modules/Notifications/"]


# ---------------------------------------------------------------------------
# Stage 3: publish
# Runs dotnet publish which performs a full incremental build internally,
# then emits the Release output to /app/publish.
# --no-restore: NuGet packages were already restored in the build stage.
# UseAppHost=false: omits the platform-specific native launcher stub
#   (not needed inside a container - the ENTRYPOINT calls dotnet directly).
# The RemoveSymbolsFromPublish MSBuild target in ParkingApp.API.csproj
# strips native *.pdb / *.dbg / *.dwarf files to reduce the image size.
# ---------------------------------------------------------------------------
FROM build AS publish
ARG BUILD_CONFIGURATION=Release

RUN dotnet publish "src/ParkingApp.API/ParkingApp.API.csproj" \
    --no-restore \
    -c "${BUILD_CONFIGURATION}" \
    -o /app/publish \
    /p:UseAppHost=false

# ---------------------------------------------------------------------------
# Stage 4: final
# Minimal ASP.NET Core 9 runtime image. Contains only the published output.
# No SDK, no source code, no test projects, no secrets.
# ---------------------------------------------------------------------------
FROM base AS final
WORKDIR /app

COPY --from=publish /app/publish .

# Cloud Run / PORT configuration
# --------------------------------
# Cloud Run injects the PORT environment variable at runtime.
# Program.cs reads PORT and calls builder.WebHost.UseUrls("http://0.0.0.0:{PORT}")
# when the variable is set, overriding ASPNETCORE_URLS.
#
# ASPNETCORE_URLS here acts as the fallback for non-Cloud-Run environments
# (e.g. local docker run without -e PORT=...).
#
# Both approaches converge on 0.0.0.0 - required for Cloud Run ingress.
# TLS termination is handled by Cloud Run's managed HTTPS load balancer.
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Run as the non-root 'app' user that is pre-created in the aspnet:9.0 base image.
USER app

ENTRYPOINT ["dotnet", "ParkingApp.API.dll"]