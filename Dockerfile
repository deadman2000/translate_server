FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build

WORKDIR /app

COPY *.sln .
COPY TranslateServer/*.csproj ./TranslateServer/
RUN dotnet restore

COPY TranslateServer/. ./TranslateServer/
WORKDIR /app/TranslateServer
RUN dotnet publish -c Release -o out


FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS runtime

WORKDIR /app
EXPOSE 80
COPY --from=build /app/TranslateServer/out ./
ENTRYPOINT ["dotnet", "TranslateServer.dll"]
