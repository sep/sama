FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
WORKDIR /build

# Copy csproj and restore as distinct layers
COPY sama/sama.csproj ./
RUN dotnet restore

# Copy everything else and build
COPY sama/ ./
RUN dotnet publish -c Release -o out --no-restore /p:MvcRazorCompileOnPublish=true

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:6.0
WORKDIR /opt/sama
COPY --from=build-env /build/out .
ENTRYPOINT ["dotnet", "sama.dll"]
ENV ASPNETCORE_ENVIRONMENT=Docker

# ------ CONFIGURATION:

# Mount this volume for DB persistence:
VOLUME /opt/sama-docker

# Set this to "true" when launching container if running behind reverse proxy:
ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=false

# Change this to bind to a specific interface or different port, if needed:
ENV ASPNETCORE_URLS="http://*:80"
