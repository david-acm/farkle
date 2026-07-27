FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Build context is the repo root (see docker-compose.yml `context: .`). Copy the
# solution-wide build config + every project file in WebApp's reference graph
# first, so `dotnet restore` is cached independently of source changes.
COPY ["global.json", "Directory.Build.props", "./"]
COPY ["src/HotDice.Shared/HotDice.Shared.csproj", "src/HotDice.Shared/"]
COPY ["src/HotDice.ApiClient/HotDice.ApiClient.csproj", "src/HotDice.ApiClient/"]
COPY ["src/Blazor.Dice/Blazor.Dice.csproj", "src/Blazor.Dice/"]
COPY ["src/HotDice/HotDice.csproj", "src/HotDice/"]
COPY ["src/HotDice.Infrastructure/HotDice.Infrastructure.csproj", "src/HotDice.Infrastructure/"]
COPY ["src/WebApp.Client/WebApp.Client.csproj", "src/WebApp.Client/"]
COPY ["src/WebApp/WebApp.csproj", "src/WebApp/"]
RUN dotnet restore "src/WebApp/WebApp.csproj"

COPY . .
RUN dotnet build "src/WebApp/WebApp.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
# Version stamped from the host's MinVer (the build can't read .git — it's .dockerignore'd).
# /p:Version is a global property, so it stamps the whole graph (WebApp + WebApp.Client),
# so the WASM UI footer gets the version too. Defaults keep local image builds working.
ARG VERSION=0.0.0-local
ARG INFORMATIONAL=0.0.0-local
RUN dotnet publish "src/WebApp/WebApp.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false \
    /p:Version="$VERSION" /p:InformationalVersion="$INFORMATIONAL"

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WebApp.dll"]
