FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8001

# ---- Build stage (SDK 8) ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Directory.Packages.props ./
COPY sp2023-mis421-mockinterviews/sp2023-mis421-mockinterviews.csproj ./

RUN dotnet restore ./sp2023-mis421-mockinterviews.csproj

COPY sp2023-mis421-mockinterviews/ ./

RUN dotnet build ./sp2023-mis421-mockinterviews.csproj -c Release -o /app/build --no-restore

# ---- Publish stage ----
FROM build AS publish
RUN dotnet publish ./sp2023-mis421-mockinterviews.csproj -c Release -o /app/publish \
    --no-restore /p:UseAppHost=false

# ---- Final runtime image ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish ./

ENV ASPNETCORE_URLS=http://+:8001

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -fsS http://localhost:8001/health || exit 1

ENTRYPOINT ["dotnet", "sp2023-mis421-mockinterviews.dll"]
