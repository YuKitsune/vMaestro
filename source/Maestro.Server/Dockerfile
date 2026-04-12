FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081
VOLUME ["/app/data"]

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["source/Maestro.Server/Maestro.Server.csproj", "Maestro.Server/"]
COPY ["source/Maestro.Server.Contracts/Maestro.Server.Contracts.csproj", "Maestro.Server.Contracts/"]
RUN dotnet restore "Maestro.Server/Maestro.Server.csproj"
COPY source/ .
WORKDIR "/src/Maestro.Server"
RUN dotnet build "Maestro.Server.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Maestro.Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Maestro.Server.dll"]
