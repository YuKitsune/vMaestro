FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM node:18-alpine AS docs-build
WORKDIR /docs
COPY ["docs/package.json", "docs/package-lock.json", "./"]
RUN npm ci
COPY docs .
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /repo

# Copy build system files
COPY [".nuke", ".nuke/"]
COPY ["build.sh", "build.cmd", "build.ps1", "./"]
COPY ["source/build/", "source/build/"]

# Copy git repository for GitVersion
COPY [".git", ".git/"]

# Copy source code
COPY ["source/", "source/"]

# Run NUKE build to publish server with proper versioning
RUN chmod +x build.sh && \
    ./build.sh PublishServer --configuration $BUILD_CONFIGURATION

FROM base AS final
ARG BUILD_CONFIGURATION=Release
WORKDIR /app
COPY --from=build /repo/source/Maestro.Server/bin/${BUILD_CONFIGURATION}/net9.0/publish .
COPY --from=docs-build /docs/build ./wwwroot
ENTRYPOINT ["dotnet", "Maestro.Server.dll"]
