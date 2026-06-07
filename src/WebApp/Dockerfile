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
COPY ["src/Farkle.Contracts/Farkle.Contracts.csproj", "src/Farkle.Contracts/"]
COPY ["src/Farkle.SharedKernel/Farkle.SharedKernel.csproj", "src/Farkle.SharedKernel/"]
COPY ["src/Farkle.ApiClient/Farkle.ApiClient.csproj", "src/Farkle.ApiClient/"]
COPY ["src/Farkle/Farkle.csproj", "src/Farkle/"]
COPY ["src/WebApp.Client/WebApp.Client.csproj", "src/WebApp.Client/"]
COPY ["src/WebApp/WebApp.csproj", "src/WebApp/"]
RUN dotnet restore "src/WebApp/WebApp.csproj"

COPY . .
RUN dotnet build "src/WebApp/WebApp.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "src/WebApp/WebApp.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WebApp.dll"]
