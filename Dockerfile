FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /app

COPY global.json nuget.config ./
COPY TranslateServer/*.csproj ./TranslateServer/
RUN dotnet restore TranslateServer/TranslateServer.csproj

COPY TranslateServer/. ./TranslateServer/
WORKDIR /app/TranslateServer
RUN dotnet publish -c Release --no-restore -o out /p:UseAppHost=false


FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app
EXPOSE 80
ENV ASPNETCORE_URLS=http://+:80
COPY --from=build /app/TranslateServer/out ./
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s CMD curl -f http://localhost/ || exit 1
ENTRYPOINT ["dotnet", "TranslateServer.dll"]
