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
WORKDIR /src
COPY ["source/Maestro.Server/Maestro.Server.csproj", "Maestro.Server/"]
RUN dotnet restore "Maestro.Server/Maestro.Server.csproj"
COPY source .
WORKDIR "/src/Maestro.Server"
RUN dotnet build "./Maestro.Server.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Maestro.Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY --from=docs-build /docs/build ./wwwroot
ENTRYPOINT ["dotnet", "Maestro.Server.dll"]
