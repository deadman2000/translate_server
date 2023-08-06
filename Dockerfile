FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build

WORKDIR /app

COPY *.sln .
COPY TranslateServer/*.csproj ./TranslateServer/
RUN dotnet restore

COPY TranslateServer/. ./TranslateServer/
WORKDIR /app/TranslateServer
RUN dotnet publish -c Release -o out


FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS runtime

WORKDIR /app
EXPOSE 80
COPY --from=build /app/TranslateServer/out ./
RUN apt update && apt install -y curl
HEALTHCHECK --interval=1s --timeout=5s CMD curl -f http://localhost/ || exit 1
ENTRYPOINT ["dotnet", "TranslateServer.dll"]
