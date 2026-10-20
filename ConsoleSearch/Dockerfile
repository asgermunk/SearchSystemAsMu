# Build from the solution root, since ConsoleSearch references Shared:
#   docker build -f ConsoleSearch/Dockerfile -t search-console .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Shared/Shared.csproj Shared/
COPY ConsoleSearch/ConsoleSearch.csproj ConsoleSearch/
RUN dotnet restore ConsoleSearch/ConsoleSearch.csproj
COPY Shared/ Shared/
COPY ConsoleSearch/ ConsoleSearch/
RUN dotnet publish ConsoleSearch/ConsoleSearch.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app/publish .
# First argument is the API base URL; it is an interactive REPL, so run it with -it.
ENTRYPOINT ["dotnet", "ConsoleSearch.dll"]
