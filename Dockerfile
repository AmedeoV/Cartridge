# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["Cartridge.sln", "./"]
COPY ["src/Cartridge.Web/Cartridge.Web.csproj", "src/Cartridge.Web/"]
COPY ["src/Cartridge.Core/Cartridge.Core.csproj", "src/Cartridge.Core/"]
COPY ["src/Cartridge.Infrastructure/Cartridge.Infrastructure.csproj", "src/Cartridge.Infrastructure/"]
COPY ["src/Cartridge.Shared/Cartridge.Shared.csproj", "src/Cartridge.Shared/"]

# Restore dependencies
RUN dotnet restore "src/Cartridge.Web/Cartridge.Web.csproj"

# Copy everything else
COPY . .

# Build the application
WORKDIR "/src/src/Cartridge.Web"
RUN dotnet build "Cartridge.Web.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "Cartridge.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Install SQLite for GOG database reading (if needed)
RUN apt-get update && \
    apt-get install -y sqlite3 libsqlite3-dev && \
    rm -rf /var/lib/apt/lists/*

# Copy published application
COPY --from=publish /app/publish .

# Create directory for uploaded databases
RUN mkdir -p /app/data

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Expose port
EXPOSE 8080

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "Cartridge.Web.dll"]
