FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Roveltia.Web/Roveltia.Web.csproj Roveltia.Web/
RUN dotnet restore Roveltia.Web/Roveltia.Web.csproj

COPY Roveltia.Web/ Roveltia.Web/
RUN dotnet publish Roveltia.Web/Roveltia.Web.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Roveltia.Web.dll"]
