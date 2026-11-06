# Build context is the repository root, see docker-compose.yml.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo
COPY Directory.Build.props Directory.Packages.props ./
COPY mock-sl/src/ mock-sl/src/
RUN dotnet publish mock-sl/src/MockSl.Api/MockSl.Api.csproj --configuration Release --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "MockSl.Api.dll"]
