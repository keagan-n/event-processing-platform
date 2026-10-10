# Build context is the repo root:
#   docker build -f src/Platform/Worker/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/ .
RUN dotnet publish Platform/Worker/Platform.Worker.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine@sha256:c13f5945240d31ae6cdc99e2c7d48c355d3e78bc98ef1415e9482ce193eef32e AS runtime
RUN apk upgrade --no-cache
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "Platform.Worker.dll"]
