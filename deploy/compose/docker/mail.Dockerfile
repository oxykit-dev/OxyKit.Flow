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
COPY docker/mail-entrypoint.sh /app/mail-entrypoint.sh
RUN chmod +x /app/mail-entrypoint.sh
ENV ASPNETCORE_URLS=http://+:5026
ENV MailTemplates__TemplatesRoot=/data/templates
ENV OXYKIT_MAIL_TEMPLATES_SEED=/app/templates
EXPOSE 5026
ENTRYPOINT ["/app/mail-entrypoint.sh"]
