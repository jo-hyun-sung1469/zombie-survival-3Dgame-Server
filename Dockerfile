FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0.301-noble@sha256:ea8bde36c11b6e7eec2656d0e59101d4462f6bd630730f2c8201ed0572b295d5 AS build
ARG TARGETARCH
WORKDIR /src

COPY zombie_servival-3Dgame_Server/zombie_survival-3Dgame_Server.csproj zombie_servival-3Dgame_Server/
RUN dotnet restore zombie_servival-3Dgame_Server/zombie_survival-3Dgame_Server.csproj -a "$TARGETARCH"

COPY zombie_servival-3Dgame_Server/ zombie_servival-3Dgame_Server/
RUN dotnet publish zombie_servival-3Dgame_Server/zombie_survival-3Dgame_Server.csproj \
    --configuration Release \
    --no-restore \
    -a "$TARGETARCH" \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.10-noble@sha256:f1126d438ccc359f51cc6d4701a8deae513856cf10f5fe645d29ea6403dcac6b AS runtime
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && apt-get clean \
    && find /var/lib/apt/lists -type f -delete

WORKDIR /app
COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0
EXPOSE 8080

USER $APP_UID
HEALTHCHECK --interval=10s --timeout=5s --start-period=20s --retries=6 \
    CMD curl --fail --silent --show-error http://127.0.0.1:8080/health || exit 1

ENTRYPOINT ["dotnet", "zombie_survival-3Dgame_Server.dll"]
