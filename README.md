# GoRide.Notification

ASP.NET Core 10 microservice — **Notification Service** for the GoRide platform.

Handles system notifications, push notifications, and Kafka event consumers for trip and location alerts.

## Folder structure

```
goride-notification/
├── .github/workflows/ci.yml        ← build + test + docker build on every push
├── src/GoRide.Notification/
│   ├── Controllers/                 ← HTTP request handling only, no business logic
│   │   └── HealthController.cs      ← GET /health — proves DB connectivity
│   ├── Services/                    ← business logic goes here
│   ├── Models/                      ← C# classes representing your data
│   ├── Data/                        ← ADO.NET data access
│   │   ├── IDbConnectionFactory.cs
│   │   └── MySqlConnectionFactory.cs
│   ├── Events/                      ← Kafka consumers / event handling
│   ├── Program.cs                   ← app startup: DI, CORS, Swagger, routing
│   ├── appsettings.json             ← non-secret config
│   ├── appsettings.Development.json ← local secret (gitignored)
│   └── GoRide.Notification.csproj
├── tests/GoRide.Notification.Tests/
├── Dockerfile
├── docker-compose.yml               ← port 8083:8080 (avoid collision with other services)
├── .env.example
├── .gitignore / .dockerignore
└── GoRide.Notification.sln
```

## Getting started

1. **Fill in config.** Copy `.env.example` to `.env` and fill in real DB credentials + Vercel URL. Also update `appsettings.json` with the correct `Db:Database` and `Db:User`.

2. **Run locally:**
   ```bash
   dotnet restore
   dotnet build
   dotnet test              # should show passing sanity test
   dotnet run --project src/GoRide.Notification/GoRide.Notification.csproj
   ```
   Visit `http://localhost:5000/health` — you should see `{"status":"healthy","database":"connected",...}`.

3. **Run via Docker:**
   ```bash
   docker compose up --build
   ```
   Same `/health` response, containerised on port `8083`.

## Why ADO.NET, not an ORM

`MySqlConnectionFactory` returns a raw `MySqlConnection` — every query uses parameterised `MySqlCommand` objects directly. This is a deliberate, explicit requirement of the assignment brief — keep it consistent across all services.
