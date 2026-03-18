# KnowledgeHub API Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files
COPY src/KnowledgeHub.Domain.Shared/*.csproj src/KnowledgeHub.Domain.Shared/
COPY src/KnowledgeHub.Domain/*.csproj src/KnowledgeHub.Domain/
COPY src/KnowledgeHub.Application.Contracts/*.csproj src/KnowledgeHub.Application.Contracts/
COPY src/KnowledgeHub.Application/*.csproj src/KnowledgeHub.Application/
COPY src/KnowledgeHub.EntityFrameworkCore/*.csproj src/KnowledgeHub.EntityFrameworkCore/
COPY src/KnowledgeHub.HttpApi/*.csproj src/KnowledgeHub.HttpApi/
COPY src/KnowledgeHub.HttpApi.Host/*.csproj src/KnowledgeHub.HttpApi.Host/
COPY common.props ./

# Restore dependencies
RUN dotnet restore src/KnowledgeHub.HttpApi.Host/KnowledgeHub.HttpApi.Host.csproj

# Copy all source files
COPY . .

# Build the application
WORKDIR /src/src/KnowledgeHub.HttpApi.Host
RUN dotnet build -c Release -o /app/build

# Publish the application
RUN dotnet publish -c Release -o /app/publish

# Publish DbMigrator
WORKDIR /src/src/KnowledgeHub.DbMigrator
RUN dotnet publish -c Release -o /app/publish

# Generate self-signed certificate for OpenIddict
RUN dotnet dev-certs https -v -ep /app/openiddict.pfx -p YOUR_CERTIFICATE_PASSPHRASE_HERE

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install timezone data and curl for health checks
RUN apt-get update && apt-get install -y --no-install-recommends \
    tzdata \
    curl \
    && rm -rf /var/lib/apt/lists/*

ENV TZ=Asia/Shanghai
ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .
COPY --from=build /app/openiddict.pfx .

# Expose port
EXPOSE 80

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
    CMD curl -f http://localhost/health-status || exit 1

ENTRYPOINT ["dotnet", "KnowledgeHub.HttpApi.Host.dll"]
