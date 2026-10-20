# Build from the solution root, since LoadBalancer references Shared:
#   docker build -f LoadBalancer/Dockerfile -t search-loadbalancer .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Shared/Shared.csproj Shared/
COPY LoadBalancer/LoadBalancer.csproj LoadBalancer/
RUN dotnet restore LoadBalancer/LoadBalancer.csproj
COPY Shared/ Shared/
COPY LoadBalancer/ LoadBalancer/
RUN dotnet publish LoadBalancer/LoadBalancer.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
# Shards are configured with Search__Backends__0, Search__Backends__1, ...
EXPOSE 8080
ENTRYPOINT ["dotnet", "LoadBalancer.dll"]
