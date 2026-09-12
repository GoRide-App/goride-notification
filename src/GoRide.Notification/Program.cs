using Microsoft.EntityFrameworkCore;
using GoRide.Notification.Data;
using GoRide.Notification.Events.Consumers;
using GoRide.Notification.Services;

var builder = WebApplication.CreateBuilder(args);

// ---- Controllers + Swagger ----
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ---- Database (ADO.NET connection factory + Entity Framework Core DbContext) ----
builder.Services.AddScoped<IDbConnectionFactory, MySqlConnectionFactory>();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    var host = MySqlConnectionFactory.GetConfigValue(builder.Configuration, "Db:Host", "DB_HOST") ?? "localhost";
    var port = MySqlConnectionFactory.GetConfigValue(builder.Configuration, "Db:Port", "DB_PORT") ?? "3306";
    var dbName = MySqlConnectionFactory.GetConfigValue(builder.Configuration, "Db:Database", "DB_DATABASE") ?? "notification_db";
    var user = MySqlConnectionFactory.GetConfigValue(builder.Configuration, "Db:User", "DB_USER") ?? "root";
    var pass = MySqlConnectionFactory.GetConfigValue(builder.Configuration, "Db:Password", "DB_PASSWORD") ?? "password";

    connectionString = $"Server={host};Port={port};Database={dbName};Uid={user};Pwd={pass};";
}

// Register AppDbContext with MySql / Pomelo (or In-Memory fallback if configured)
if (builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseInMemoryDatabase("NotificationTestDb"));
}
else
{
    builder.Services.AddDbContext<AppDbContext>(options =>
    {
        try
        {
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
        }
        catch
        {
            // Fallback for environment setup prior to MySQL migration initialization
            options.UseInMemoryDatabase("NotificationDevDb");
        }
    });
}

// ---- Application Services DI ----
builder.Services.AddScoped<IPreferenceService, PreferenceService>();
builder.Services.AddScoped<IPushSender, FcmPushSender>();
builder.Services.AddScoped<INotificationDispatcher, NotificationDispatcher>();

// ---- Kafka Consumer Hosted Background Service ----
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddHostedService<TripEventConsumerService>();
}

// ---- CORS configuration for Next.js Frontend ----
var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? Array.Empty<string>();

var origins = configuredOrigins
    .Where(o => !string.IsNullOrWhiteSpace(o) && !o.Contains("${"))
    .ToList();

if (!origins.Contains("http://localhost:3000", StringComparer.OrdinalIgnoreCase))
{
    origins.Add("http://localhost:3000");
}
if (!origins.Contains("http://127.0.0.1:3000", StringComparer.OrdinalIgnoreCase))
{
    origins.Add("http://127.0.0.1:3000");
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy.WithOrigins(origins.ToArray())
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Auto-create database schema if in development and using in-memory or raw DB
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        db.Database.EnsureCreated();
    }
    catch
    {
        // Connection will be established when DB server is active
    }
}

// ---- Swagger UI (dev only) ----
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("FrontendPolicy");
app.UseAuthorization();
app.MapControllers();

app.Run();

// Exposes generated Program class for WebApplicationFactory integration testing
public partial class Program { }
