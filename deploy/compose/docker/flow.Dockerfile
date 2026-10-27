# Build context: suite root. Flow uses OxyKit.Render.Client from nuget.org (PackageReference).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY OxyKit.Flow/src/OxyKit.Flow.Abstractions/ OxyKit.Flow/src/OxyKit.Flow.Abstractions/
COPY OxyKit.Flow/src/OxyKit.Flow.Infrastructure/ OxyKit.Flow/src/OxyKit.Flow.Infrastructure/
COPY OxyKit.Flow/src/OxyKit.Flow.Infrastructure.Sqlite/ OxyKit.Flow/src/OxyKit.Flow.Infrastructure.Sqlite/
COPY OxyKit.Flow/src/OxyKit.Flow.Infrastructure.Postgres/ OxyKit.Flow/src/OxyKit.Flow.Infrastructure.Postgres/
COPY OxyKit.Flow/src/OxyKit.Flow/ OxyKit.Flow/src/OxyKit.Flow/
WORKDIR /src/OxyKit.Flow/src/OxyKit.Flow
RUN dotnet restore OxyKit.Flow.csproj \
    && dotnet publish OxyKit.Flow.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:5077
EXPOSE 5077
ENTRYPOINT ["dotnet", "OxyKit.Flow.dll"]
