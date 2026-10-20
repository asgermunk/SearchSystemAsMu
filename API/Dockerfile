# Build from the solution root, since API references Shared:
#   docker build -f API/Dockerfile -t search-api .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Shared/Shared.csproj Shared/
COPY API/API.csproj API/
RUN dotnet restore API/API.csproj
COPY Shared/ Shared/
COPY API/ API/
RUN dotnet publish API/API.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
# The shard's SQLite file is mounted here; point at it with Search__SqliteDatabasePath
VOLUME /db
EXPOSE 8080
ENTRYPOINT ["dotnet", "API.dll"]
