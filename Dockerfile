# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy CPM and solution files first for layer caching
COPY ["Directory.Build.props", "."]
COPY ["Directory.Packages.props", "."]
COPY ["CebizPay.slnx", "."]

# Copy csproj files
COPY ["src/CebizPay.Api/CebizPay.Api.csproj", "src/CebizPay.Api/"]
COPY ["src/CebizPay.Application/CebizPay.Application.csproj", "src/CebizPay.Application/"]
COPY ["src/CebizPay.Domain/CebizPay.Domain.csproj", "src/CebizPay.Domain/"]
COPY ["src/CebizPay.Infrastructure/CebizPay.Infrastructure.csproj", "src/CebizPay.Infrastructure/"]
COPY ["src/CebizPay.Workers/CebizPay.Workers.csproj", "src/CebizPay.Workers/"]

# Restore packages
RUN dotnet restore "src/CebizPay.Api/CebizPay.Api.csproj"

# Copy source code
COPY . .

# Publish app
WORKDIR "/src/src/CebizPay.Api"
RUN dotnet publish "CebizPay.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Run as non-root user
USER $APP_UID

COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "CebizPay.Api.dll"]
