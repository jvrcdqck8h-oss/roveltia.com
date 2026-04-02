# ASP.NET Core + PostgreSQL in one container (data under PGDATA, use a volume).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Roveltia.Web/Roveltia.Web.csproj Roveltia.Web/
RUN dotnet restore Roveltia.Web/Roveltia.Web.csproj

COPY Roveltia.Web/ Roveltia.Web/
RUN dotnet publish Roveltia.Web/Roveltia.Web.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
USER root
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends postgresql postgresql-client \
    && rm -rf /var/lib/apt/lists/*

ENV PGDATA=/var/lib/postgresql/roveltia-data \
    POSTGRES_USER=roveltia \
    POSTGRES_DB=roveltia

COPY docker/docker-entrypoint.sh /docker-entrypoint.sh
RUN chmod +x /docker-entrypoint.sh

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

ENTRYPOINT ["/docker-entrypoint.sh"]
