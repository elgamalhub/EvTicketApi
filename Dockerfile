# ---------- Web build ----------
FROM node:22-alpine AS web
WORKDIR /src/web
COPY web/package.json web/package-lock.json ./
RUN npm ci --legacy-peer-deps
COPY web/ ./
RUN npm run build -- --configuration production

# ---------- API publish ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src
COPY api/EvTicket.Api/EvTicket.Api.csproj api/EvTicket.Api/
RUN dotnet restore api/EvTicket.Api/EvTicket.Api.csproj
COPY api/ ./api/
COPY --from=web /src/web/dist/evticket-web/browser ./api/EvTicket.Api/wwwroot
RUN dotnet publish api/EvTicket.Api/EvTicket.Api.csproj -c Release -o /app/publish --no-restore

# ---------- Runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=api /app/publish ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "EvTicket.Api.dll"]
