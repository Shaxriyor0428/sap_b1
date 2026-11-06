# Build context is the repository root, see docker-compose.yml.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo
COPY Directory.Build.props Directory.Packages.props ./
COPY api/src/ api/src/
RUN dotnet publish api/src/Sap.Api/Sap.Api.csproj --configuration Release --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "Sap.Api.dll"]
