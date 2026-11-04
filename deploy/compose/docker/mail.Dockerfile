# Build context: suite root. Mail uses Observability packages from nuget.org.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY OxyKit.Mail/src/ OxyKit.Mail/src/
WORKDIR /src/OxyKit.Mail/src/OxyKit.Mail
RUN dotnet restore OxyKit.Mail.csproj \
    && dotnet publish OxyKit.Mail.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:5026
EXPOSE 5026
ENTRYPOINT ["dotnet", "OxyKit.Mail.dll"]
