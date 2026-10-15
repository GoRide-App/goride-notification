using MySqlConnector;

namespace GoRide.Notification.Data;

/// <summary>
/// Builds the MySQL connection string from separate config keys (Db:Host, Db:Port, etc.)
/// or environment variables (DB_HOST, DB_PORT, etc., including .env file resolution),
/// falling back safely if port or placeholders are unparsed.
/// </summary>
public class MySqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public MySqlConnectionFactory(IConfiguration configuration)
    {
        var sslModeRaw = GetConfigValue(configuration, "Db:SslMode", "DB_SSLMODE") ?? "Required";
        var host = GetConfigValue(configuration, "Db:Host", "DB_HOST") ?? "localhost";
        var portStr = GetConfigValue(configuration, "Db:Port", "DB_PORT") ?? "3306";
        var database = GetConfigValue(configuration, "Db:Database", "DB_DATABASE") ?? "notification_db";
        var user = GetConfigValue(configuration, "Db:User", "DB_USER") ?? "root";
        var password = GetConfigValue(configuration, "Db:Password", "DB_PASSWORD") ?? "";

        if (!uint.TryParse(portStr, out var port))
        {
            port = 3306;
        }

        var csBuilder = new MySqlConnectionStringBuilder
        {
            Server = host,
            Port = port,
            Database = database,
            UserID = user,
            Password = password,
            SslMode = Enum.TryParse<MySqlSslMode>(sslModeRaw, true, out var sslMode) ? sslMode : MySqlSslMode.Required,
        };

        _connectionString = csBuilder.ConnectionString;
    }

    public MySqlConnection CreateConnection() => new MySqlConnection(_connectionString);

    internal static string? GetConfigValue(IConfiguration configuration, string configKey, string envKey)
    {
        // 1. Direct environment variable (e.g. DB_PASSWORD or Db__Password)
        var envVal = Environment.GetEnvironmentVariable(envKey)
                  ?? Environment.GetEnvironmentVariable(configKey.Replace(":", "__"));
        if (!string.IsNullOrWhiteSpace(envVal))
        {
            return envVal;
        }

        // 2. Configuration value from appsettings.json / appsettings.Development.json
        var cfgVal = configuration[configKey];
        if (!string.IsNullOrWhiteSpace(cfgVal) && !cfgVal.StartsWith("${"))
        {
            return cfgVal;
        }

        // 3. Fallback: Parse .env file if present in current or parent directories
        var dotenvVal = ReadFromDotEnv(envKey);
        if (!string.IsNullOrWhiteSpace(dotenvVal))
        {
            return dotenvVal;
        }

        return null;
    }

    private static string? ReadFromDotEnv(string envKey)
    {
        try
        {
            var dir = Directory.GetCurrentDirectory();
            while (!string.IsNullOrEmpty(dir))
            {
                var envPath = Path.Combine(dir, ".env");
                if (File.Exists(envPath))
                {
                    foreach (var line in File.ReadAllLines(envPath))
                    {
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("#") || !trimmed.Contains('=')) continue;
                        var parts = trimmed.Split('=', 2);
                        if (string.Equals(parts[0].Trim(), envKey, StringComparison.OrdinalIgnoreCase))
                        {
                            return parts[1].Trim(' ', '"', '\'');
                        }
                    }
                }
                var parent = Directory.GetParent(dir);
                if (parent == null) break;
                dir = parent.FullName;
            }
        }
        catch
        {
            // Ignore file read errors
        }
        return null;
    }
}
