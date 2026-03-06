# Set default build configuration
ARG BUILD_CONFIGURATION=Release

FROM --platform=$BUILDPLATFORM node:24-slim AS client
ARG BUILD_CONFIGURATION

# Step 1: Prepare Client
COPY ./Lingarr.Client/ /Client
WORKDIR /Client
# Install dependencies
RUN npm ci
RUN npm run build

# Step 2: Prepare the base image for the server
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
ARG TARGETARCH
WORKDIR /app

# Step 3: Build the server
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION
ARG TARGETARCH
WORKDIR /src
COPY ["Lingarr.Server/Lingarr.Server.csproj", "Lingarr.Server/"]
RUN dotnet restore "./Lingarr.Server/./Lingarr.Server.csproj" --arch $TARGETARCH
COPY . .
WORKDIR "/src/Lingarr.Server"
COPY --from=client /Client/dist/ ./wwwroot
RUN dotnet build "./Lingarr.Server.csproj" -c $BUILD_CONFIGURATION -o /app/build --arch $TARGETARCH

# Step 4: Publish the application
FROM --platform=$BUILDPLATFORM build AS publish
ARG BUILD_CONFIGURATION
ARG TARGETARCH
RUN dotnet publish "./Lingarr.Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --arch $TARGETARCH

# Step 5: Final image
FROM base AS final

# Install curl for health checks (not included in aspnet slim base image)
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=publish /app/publish .

# Note: Non-root user is not used because host-mounted volumes at /app/config
# would have host UID/GID that the container user cannot write to.
# Consider using s6-overlay with PUID/PGID env vars for proper non-root support.

VOLUME /app/config

EXPOSE 9876

HEALTHCHECK --interval=30s --timeout=5s --start-period=60s --retries=3 \
    CMD curl -f http://localhost:9876/api/health || exit 1

ENTRYPOINT ["dotnet", "Lingarr.Server.dll"]