# Use the official .NET 9 runtime as base image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5000

# Use the SDK image to build the application
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["src/InMemoryServer/InMemoryServer.csproj", "src/InMemoryServer/"]
COPY ["src/Shared/Shared.csproj", "src/Shared/"]
COPY ["Directory.Build.props", "."]
RUN dotnet restore "src/InMemoryServer/InMemoryServer.csproj"

# Copy all source code
COPY . .
WORKDIR "/src/src/InMemoryServer"
RUN dotnet build "InMemoryServer.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "InMemoryServer.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Create final image
FROM base AS final
WORKDIR /app

# Create a non-root user
RUN adduser --disabled-password --gecos '' appuser && chown -R appuser /app
USER appuser

# Copy published application
COPY --from=publish /app/publish .

# Create battle replay directory
RUN mkdir -p ./battle_replay

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:5000/health || exit 1

ENTRYPOINT ["dotnet", "InMemoryServer.dll"]
