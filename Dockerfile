# syntax=docker/dockerfile:1.7

ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS restore
WORKDIR /src

COPY .config/dotnet-tools.json .config/dotnet-tools.json
COPY src/SeoIntelligence.Domain/SeoIntelligence.Domain.csproj src/SeoIntelligence.Domain/
COPY src/SeoIntelligence.Contracts/SeoIntelligence.Contracts.csproj src/SeoIntelligence.Contracts/
COPY src/SeoIntelligence.Application/SeoIntelligence.Application.csproj src/SeoIntelligence.Application/
COPY src/SeoIntelligence.Infrastructure/SeoIntelligence.Infrastructure.csproj src/SeoIntelligence.Infrastructure/
COPY src/SeoIntelligence.Api/SeoIntelligence.Api.csproj src/SeoIntelligence.Api/
COPY src/SeoIntelligence.Web/SeoIntelligence.Web.csproj src/SeoIntelligence.Web/
COPY src/SeoIntelligence.Worker/SeoIntelligence.Worker.csproj src/SeoIntelligence.Worker/

# Restored into the image layer rather than a BuildKit cache mount.
#
# A cache mount is not part of any layer, so it is not carried by a registry or a
# GitHub Actions layer cache. Every later stage here runs --no-restore or --no-build
# and reads what this stage produced, so a build that restores its layers from a
# remote cache and then re-executes one of those stages finds the packages and the
# local tools gone:
#
#   Run "dotnet tool restore" to make the "dotnet-ef" command available.
#   MSB3030: Could not copy the file ".../dapper/2.0.123/lib/net5.0/Dapper.dll"
#
# Reproducible with: docker build --no-cache-filter migrate-bundle .
#
# The cost is that a csproj change re-downloads. cache-from buys that back: this
# layer is reused whenever the csproj files are unchanged, which is most builds.
RUN \
    dotnet tool restore \
    && dotnet restore src/SeoIntelligence.Api/SeoIntelligence.Api.csproj \
    && dotnet restore src/SeoIntelligence.Web/SeoIntelligence.Web.csproj \
    && dotnet restore src/SeoIntelligence.Worker/SeoIntelligence.Worker.csproj

COPY src/ src/

# Shared build stage: Domain/Contracts/Application/Infrastructure compile once
# and are reused by every publish target and the migration bundle.
FROM restore AS build
ARG BUILD_CONFIGURATION=Release
RUN \
    dotnet build src/SeoIntelligence.Api/SeoIntelligence.Api.csproj --configuration ${BUILD_CONFIGURATION} --no-restore \
    && dotnet build src/SeoIntelligence.Web/SeoIntelligence.Web.csproj --configuration ${BUILD_CONFIGURATION} --no-restore \
    && dotnet build src/SeoIntelligence.Worker/SeoIntelligence.Worker.csproj --configuration ${BUILD_CONFIGURATION} --no-restore

FROM build AS publish-api
ARG BUILD_CONFIGURATION=Release
RUN \
    dotnet publish src/SeoIntelligence.Api/SeoIntelligence.Api.csproj \
    --configuration ${BUILD_CONFIGURATION} \
    --no-build \
    --output /app/publish \
    /p:UseAppHost=false

FROM build AS publish-web
ARG BUILD_CONFIGURATION=Release
RUN \
    dotnet publish src/SeoIntelligence.Web/SeoIntelligence.Web.csproj \
    --configuration ${BUILD_CONFIGURATION} \
    --no-build \
    --output /app/publish \
    /p:UseAppHost=false

FROM build AS publish-worker
ARG BUILD_CONFIGURATION=Release
RUN \
    dotnet publish src/SeoIntelligence.Worker/SeoIntelligence.Worker.csproj \
    --configuration ${BUILD_CONFIGURATION} \
    --no-build \
    --output /app/publish \
    /p:UseAppHost=false

# EF migration bundle: a self-sufficient executable so the runtime migrate image
# ships neither the SDK nor the sources. The trailing --connection placeholder is
# only used at bundle-creation time (no database access happens); at runtime the
# bundle resolves the real connection from Database__* / ConnectionStrings__Default
# environment variables via SeoIntelligenceDbContextFactory.
FROM build AS migrate-bundle
ARG BUILD_CONFIGURATION=Release
RUN \
    dotnet tool run dotnet-ef -- migrations bundle \
    --configuration ${BUILD_CONFIGURATION} \
    --no-build \
    --project src/SeoIntelligence.Infrastructure/SeoIntelligence.Infrastructure.csproj \
    --startup-project src/SeoIntelligence.Api/SeoIntelligence.Api.csproj \
    --output /app/efbundle \
    -- --connection "Host=bundle-placeholder"

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime-base
WORKDIR /app
# Single authoritative container port; matches the aspnet base-image default.
ENV ASPNETCORE_HTTP_PORTS=8080
# curl backs the Compose healthchecks for api/web.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /data/storage /app/.data/data-protection-keys \
    && chown -R app:app /data /app/.data
USER app

FROM runtime-base AS api
COPY --from=publish-api --chown=app:app /app/publish .
# Stamped so a running container can be traced back to a commit, and so the restore rehearsal can
# check mechanically that a reused image was built from the checkout it is verifying. Declared in
# the final stages rather than runtime-base: a new revision then invalidates only the last layer.
ARG SOURCE_REVISION=unknown
LABEL org.opencontainers.image.revision=$SOURCE_REVISION
EXPOSE 8080
ENTRYPOINT ["dotnet", "SeoIntelligence.Api.dll"]

FROM runtime-base AS web
COPY --from=publish-web --chown=app:app /app/publish .
ARG SOURCE_REVISION=unknown
LABEL org.opencontainers.image.revision=$SOURCE_REVISION
EXPOSE 8080
ENTRYPOINT ["dotnet", "SeoIntelligence.Web.dll"]

FROM runtime-base AS worker
COPY --from=publish-worker --chown=app:app /app/publish .
ARG SOURCE_REVISION=unknown
LABEL org.opencontainers.image.revision=$SOURCE_REVISION
ENTRYPOINT ["dotnet", "SeoIntelligence.Worker.dll"]

FROM runtime-base AS migrate
COPY --from=migrate-bundle --chown=app:app /app/efbundle .
ARG SOURCE_REVISION=unknown
LABEL org.opencontainers.image.revision=$SOURCE_REVISION
ENTRYPOINT ["./efbundle"]
