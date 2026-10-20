# Build from the solution root, since SearchFrontend references Shared:
#   docker build -f SearchFrontend/Dockerfile -t search-frontend .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Shared/Shared.csproj Shared/
COPY SearchFrontend/SearchFrontend.csproj SearchFrontend/
RUN dotnet restore SearchFrontend/SearchFrontend.csproj
COPY Shared/ Shared/
COPY SearchFrontend/ SearchFrontend/
RUN dotnet publish SearchFrontend/SearchFrontend.csproj -c Release -o /app/publish --no-restore

# Blazor WebAssembly is just static files once published - serve them with nginx.
# The browser calls the API itself, at ApiBaseUrl in wwwroot/appsettings.json.
FROM nginx:alpine
COPY SearchFrontend/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html
EXPOSE 80
