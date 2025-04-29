# Base image for runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src-app

# Copy solution and project files for restore
COPY ["./*.props", "./"]
COPY ["./*.sln", "./"]
COPY ["./src/AccidentMonitor.Domain/AccidentMonitor.Domain.csproj", "./src/AccidentMonitor.Domain/"]
COPY ["./src/AccidentMonitor.Application/AccidentMonitor.Application.csproj", "./src/AccidentMonitor.Application/"]
COPY ["./src/AccidentMonitor.Infrastructure/AccidentMonitor.Infrastructure.csproj", "./src/AccidentMonitor.Infrastructure/"]
COPY ["./src/AccidentMonitor.ServiceDefaults/AccidentMonitor.ServiceDefaults.csproj", "./src/AccidentMonitor.ServiceDefaults/"]
COPY ["./src/AccidentMonitor.WebApi/AccidentMonitor.WebApi.csproj", "./src/AccidentMonitor.WebApi/"]
COPY ["./src/AccidentMonitor.WebApp/AccidentMonitor.WebApp.csproj", "./src/AccidentMonitor.WebApp/"]
COPY ["./src/AccidentMonitor.AppHost/AccidentMonitor.AppHost.csproj", "./src/AccidentMonitor.AppHost/"]

COPY ["./tests/AccidentMonitor.Application.FunctionalTests/AccidentMonitor.Application.FunctionalTests.csproj", "./tests/AccidentMonitor.Application.FunctionalTests/"]
COPY ["./tests/AccidentMonitor.Application.UnitTests/AccidentMonitor.Application.UnitTests.csproj", "./tests/AccidentMonitor.Application.UnitTests/"]
COPY ["./tests/AccidentMonitor.Domain.UnitTests/AccidentMonitor.Domain.UnitTests.csproj", "./tests/AccidentMonitor.Domain.UnitTests/"]
COPY ["./tests/AccidentMonitor.Infrastructure.IntegrationTests/AccidentMonitor.Infrastructure.IntegrationTests.csproj", "./tests/AccidentMonitor.Infrastructure.IntegrationTests/"]

RUN dotnet restore "AccidentMonitor.sln"

# Copy entire source code and build
COPY . ./
WORKDIR /src-app/src/AccidentMonitor.WebApi
RUN dotnet build "./AccidentMonitor.WebApi.csproj" -c $BUILD_CONFIGURATION -o /accident-monitor-app/build

# Publish stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./AccidentMonitor.WebApi.csproj" -c $BUILD_CONFIGURATION -o /accident-monitor-app/publish

# Final runtime stage
FROM base AS final
WORKDIR /app
COPY --from=publish /accident-monitor-app/publish .

# Set environment variables
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV ASPNETCORE_URLS=http://+:80

# Entry point for the application
ENTRYPOINT ["dotnet", "AccidentMonitor.WebApi.dll"]