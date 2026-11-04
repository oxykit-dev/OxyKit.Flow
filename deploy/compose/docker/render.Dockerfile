# Build context: suite root. Render uses OxyKit.Observability from nuget.org.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY OxyKit.Render/src/OxyKit.Render.Contracts/ OxyKit.Render/src/OxyKit.Render.Contracts/
COPY OxyKit.Render/src/OxyKit.Render.Core/ OxyKit.Render/src/OxyKit.Render.Core/
COPY OxyKit.Render/src/OxyKit.Render.Api/ OxyKit.Render/src/OxyKit.Render.Api/
WORKDIR /src/OxyKit.Render/src/OxyKit.Render.Api
RUN dotnet restore OxyKit.Render.Api.csproj \
    && dotnet publish OxyKit.Render.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:5066
EXPOSE 5066
ENTRYPOINT ["dotnet", "OxyKit.Render.Api.dll"]
