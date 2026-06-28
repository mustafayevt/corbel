# syntax=docker/dockerfile:1
#
# Multi-stage build for the Corbel API. Build context is the REPOSITORY ROOT (see compose.yaml)
# so the central build props / package manifest and the ServiceDefaults project are available.

# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore against the central manifest first so the layer is cached unless dependencies change.
COPY global.json nuget.config Directory.Build.props Directory.Packages.props ./
COPY src/Corbel.Api/Corbel.Api.csproj src/Corbel.Api/
COPY src/Corbel.ServiceDefaults/Corbel.ServiceDefaults.csproj src/Corbel.ServiceDefaults/
RUN dotnet restore src/Corbel.Api/Corbel.Api.csproj

# Copy only what the API build needs (the props/manifests were already copied above for the restore layer);
# .editorconfig is required because EnforceCodeStyleInBuild reads it. Scoping the COPY keeps the publish layer
# cache from busting on unrelated web/test/docs edits.
COPY .editorconfig ./
COPY src/ src/
RUN dotnet publish src/Corbel.Api/Corbel.Api.csproj \
    -c Release \
    -p:ContinuousIntegrationBuild=true \
    --no-restore \
    -o /app

# ---- runtime ----
# noble-chiseled-extra: rootless and shell-free, but keeps ICU + tzdata (plain -chiseled silently forces
# invariant globalization, a trap the moment a downstream app needs cultures or TimeZoneInfo).
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra AS final
WORKDIR /app
COPY --from=build /app ./

# Kestrel listens on 8080 by default in the .NET container images; run as the image's non-root user.
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
USER $APP_UID

# No container HEALTHCHECK: the chiseled image is shell-free with no curl/wget, so readiness must be
# probed externally over HTTP at /health/ready (k8s httpGet, an LB, etc.) — see compose.yaml.
ENTRYPOINT ["dotnet", "Corbel.Api.dll"]
