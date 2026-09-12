using GoRide.Notification.Data;

var builder = WebApplication.CreateBuilder(args);

// ---- Controllers + Swagger ----
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ---- Database (ADO.NET connection factory — see Data/MySqlConnectionFactory.cs) ----
builder.Services.AddScoped<IDbConnectionFactory, MySqlConnectionFactory>();

// ---- CORS: allow the Next.js frontend (local dev + Vercel-hosted) to call this API ----
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

// ---- Swagger UI (dev only — don't expose this publicly in production) ----
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("FrontendPolicy");
app.UseAuthorization();
app.MapControllers();

app.Run();

// Exposes the generated Program class so integration tests can spin up this app
// in-memory via WebApplicationFactory<Program> later, without any extra setup.
public partial class Program { }
