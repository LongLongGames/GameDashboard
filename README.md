# GameDashboard

公司级 GM / 运营 / 程序 聚合后台（前后端分离，全公司一份）。

对齐 [LongLongGames/.github](https://github.com/LongLongGames/.github) 与 BugReport/MP 镜像规范。

## 结构

```
GameDashboard/
├── docker-compose.yml          # 与 src 同级
├── .env.example
└── src/
    ├── GameDashboard.Api/      # Minimal API · Npgsql · DbUp · AOT
    │   ├── Dockerfile          # 多阶段：React build → AOT → chiseled
    │   └── ...
    └── GameDashboard.Web/      # React + Vite
```

- **无 EF / 无 Razor**
- 容器 **8080**（勿在 appsettings 写死 11090，否则映射错位），宿主机 **11090**
- 迁移：`gamedashboard-migrate`（`--migrate` Job）

## 启动

```bash
docker compose up -d --build
# http://localhost:11090
# root / ChangeMe123!  （首次强制改密）
```

### 本地开发

```bash
docker compose up -d postgres
# 迁移一次
docker compose run --rm gamedashboard-migrate
# 或: cd src/GameDashboard.Api && dotnet run -- --migrate

# API
cd src/GameDashboard.Api && dotnet run   # :11090

# Web（另开终端，本机 Node）
cd src/GameDashboard.Web && npm install && npm run dev  # :5173，proxy → API
```

## match3

默认 `Games:Match3:BaseUrl=http://localhost:13180`。闭环页调用 `/health`、排行榜、version-check；`DebugJwt` 可 cheat-refill。
