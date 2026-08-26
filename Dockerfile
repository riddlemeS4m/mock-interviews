FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8001

# ---- Build stage (SDK 10) ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates curl \
    && rm -rf /var/lib/apt/lists/*

COPY Directory.Packages.props global.json ./
COPY .config/dotnet-tools.json .config/dotnet-tools.json
COPY scripts/ scripts/
COPY mock-interviews/mock-interviews.csproj mock-interviews/

RUN dotnet restore mock-interviews/mock-interviews.csproj

COPY mock-interviews/ mock-interviews/
WORKDIR /src/mock-interviews

RUN /src/scripts/tailwind.sh build

RUN dotnet build mock-interviews.csproj -c Release -o /app/build --no-restore

RUN dotnet tool restore \
    && dotnet ef migrations bundle --configuration Release --no-build --output /app/efbundle

# ---- Publish stage ----
FROM build AS publish
RUN dotnet publish mock-interviews.csproj -c Release -o /app/publish \
    --no-restore /p:UseAppHost=false

# ---- Final runtime image ----
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish ./
COPY --from=build /app/efbundle ./efbundle

ENV ASPNETCORE_URLS=http://+:8001

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -fsS http://localhost:8001/health || exit 1

ENTRYPOINT ["dotnet", "mock-interviews.dll"]
