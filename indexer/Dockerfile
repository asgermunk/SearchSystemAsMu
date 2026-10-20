# Build from the solution root, since indexer references Shared:
#   docker build -f indexer/Dockerfile -t search-indexer .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Shared/Shared.csproj Shared/
COPY indexer/indexer.csproj indexer/
RUN dotnet restore indexer/indexer.csproj
COPY Shared/ Shared/
COPY indexer/ indexer/
RUN dotnet publish indexer/indexer.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app/publish .
# Usage: indexer [folder-to-index] [sqlite-database-path], e.g. /data /db/AllenP.db
# It asks SQLite (1) or Postgres (2) on stdin, so run it with -i.
ENTRYPOINT ["dotnet", "indexer.dll"]
