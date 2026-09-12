# context = 仓库根目录（compose: context: .  dockerfile: src/GameDashboard.Api/Dockerfile）
# 规范：AOT + runtime-deps noble-chiseled；容器 8080
FROM node:22-alpine AS web
WORKDIR /web
COPY src/GameDashboard.Web/package.json src/GameDashboard.Web/package-lock.json* ./
RUN npm install
COPY src/GameDashboard.Web/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
RUN apt-get update && apt-get install -y clang zlib1g-dev && rm -rf /var/lib/apt/lists/*

COPY src/GameDashboard.Api/GameDashboard.Api.csproj GameDashboard.Api/
RUN dotnet restore GameDashboard.Api/GameDashboard.Api.csproj
COPY src/GameDashboard.Api/ GameDashboard.Api/
COPY --from=web /web/dist/ GameDashboard.Api/wwwroot/

WORKDIR /src/GameDashboard.Api
RUN dotnet publish GameDashboard.Api.csproj -c Release -r linux-x64 -o /app/publish -p:PublishAot=true

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["./GameDashboard.Api"]
