# syntax=docker/dockerfile:1.4

# Build stage
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
ARG BUILD_CONFIGURATION=Release
ARG TARGETARCH
ARG VERSION=dev
WORKDIR /src

# Copy project files
COPY ["src/LibraFoto.Api/LibraFoto.Api.csproj", "src/LibraFoto.Api/"]
COPY ["src/LibraFoto.ServiceDefaults/LibraFoto.ServiceDefaults.csproj", "src/LibraFoto.ServiceDefaults/"]
COPY ["src/LibraFoto.Modules.Display/LibraFoto.Modules.Display.csproj", "src/LibraFoto.Modules.Display/"]
COPY ["src/LibraFoto.Modules.Admin/LibraFoto.Modules.Admin.csproj", "src/LibraFoto.Modules.Admin/"]
COPY ["src/LibraFoto.Modules.Storage/LibraFoto.Modules.Storage.csproj", "src/LibraFoto.Modules.Storage/"]
COPY ["src/LibraFoto.Modules.Media/LibraFoto.Modules.Media.csproj", "src/LibraFoto.Modules.Media/"]
COPY ["src/LibraFoto.Modules.Auth/LibraFoto.Modules.Auth.csproj", "src/LibraFoto.Modules.Auth/"]
COPY ["src/LibraFoto.Data/LibraFoto.Data.csproj", "src/LibraFoto.Data/"]
COPY ["src/LibraFoto.Shared/LibraFoto.Shared.csproj", "src/LibraFoto.Shared/"]

RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore "src/LibraFoto.Api/LibraFoto.Api.csproj" -a $TARGETARCH

# Copy source
COPY . .

WORKDIR "/src/src/LibraFoto.Api"
RUN dotnet build "LibraFoto.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
ARG TARGETARCH
RUN dotnet publish "LibraFoto.Api.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    --self-contained \
    -a $TARGETARCH \
    /p:PublishSingleFile=false

# Runtime stage - using standard runtime-deps for compatibility
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-preview AS final
ARG VERSION=dev
WORKDIR /app
EXPOSE 8080

# Add version labels
LABEL version="${VERSION}" \
      name="LibraFoto API" \
      description="LibraFoto photo management API"

ENV LIBRAFOTO_VERSION=${VERSION}

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Create data directory with proper permissions
RUN mkdir -p /data && chmod 777 /data

COPY --from=publish /app/publish .
ENTRYPOINT ["./LibraFoto.Api"]
