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
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish ./

# Copy source files needed for EF migrations
COPY --from=build /src/ ./src/
COPY Directory.Packages.props ./

ENV ASPNETCORE_URLS=http://+:8001

RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -fsS http://localhost:8001/health || exit 1

# Create startup script that runs migrations then starts the app
RUN echo '#!/bin/bash\n\
set -e\n\
echo "Running database migrations..."\n\
cd /app/src\n\
\n\
# Install and restore EF tools locally\n\
echo "Installing EF Core tools..."\n\
dotnet tool install dotnet-ef --version 8.0.11 --tool-path ./tools || echo "EF tools already installed"\n\
export PATH="$PATH:/app/src/tools"\n\
\n\
echo "EF Core tools version:"\n\
./tools/dotnet-ef --version || echo "EF tools not available"\n\
\n\
echo "Running Users context migration..."\n\
./tools/dotnet-ef database update --context UsersDbContext --startup-project . --no-build --verbose || echo "Users migration completed or failed"\n\
\n\
echo "Running MockInterviews context migration..."\n\
./tools/dotnet-ef database update --context MockInterviewsDbContext --startup-project . --no-build --verbose || echo "MockInterviews migration completed or failed"\n\
\n\
echo "Starting application..."\n\
cd /app\n\
exec dotnet sp2023-mis421-mockinterviews.dll' > /app/start.sh && chmod +x /app/start.sh

ENTRYPOINT ["/app/start.sh"]
