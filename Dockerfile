# syntax=docker/dockerfile:1
# Image: ghcr.io/oxykit-dev/oxykit-flow (published by .github/workflows/docker.yml).
# Framework-dependent portable publish, so the build stage runs natively for every target platform.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/OxyKit.Flow.Abstractions/ src/OxyKit.Flow.Abstractions/
COPY src/OxyKit.Flow.Infrastructure/ src/OxyKit.Flow.Infrastructure/
COPY src/OxyKit.Flow.Infrastructure.Sqlite/ src/OxyKit.Flow.Infrastructure.Sqlite/
COPY src/OxyKit.Flow.Infrastructure.Postgres/ src/OxyKit.Flow.Infrastructure.Postgres/
COPY src/OxyKit.Flow/ src/OxyKit.Flow/
RUN dotnet publish src/OxyKit.Flow/OxyKit.Flow.csproj -c Release -o /app/publish \
    && rm -f /app/publish/appsettings.Development.json

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:5077
EXPOSE 5077
HEALTHCHECK --interval=10s --timeout=5s --start-period=20s --retries=6 \
    CMD curl -f http://localhost:5077/health || exit 1
ENTRYPOINT ["dotnet", "OxyKit.Flow.dll"]
