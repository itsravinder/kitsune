# ============================================================
# KITSUNE Backend – Multi-stage Production Dockerfile
# ============================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Kitsune.Backend.csproj", "."]
RUN dotnet restore --runtime linux-x64
COPY . .
RUN dotnet publish "Kitsune.Backend.csproj" -c Release -r linux-x64 --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production
COPY --from=build /app/publish .
RUN groupadd -r kitsune && useradd -r -g kitsune kitsune && chown -R kitsune:kitsune /app
USER kitsune
HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
    CMD curl -f http://localhost:5000/health || exit 1
ENTRYPOINT ["dotnet", "Kitsune.Backend.dll"]
