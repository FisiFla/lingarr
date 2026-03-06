using Microsoft.EntityFrameworkCore;

namespace Lingarr.Core.Configuration;

public static class DatabaseConfiguration
{
    private const string ContainerConfigPath = "/app/config";

    /// <summary>
    /// Gets the current database connection type from environment.
    /// Defaults to sqlite
    /// </summary>
    public static string GetDbConnection()
    {
        return Environment.GetEnvironmentVariable("DB_CONNECTION")?.ToLower() ?? "sqlite";
    }

    /// <summary>
    /// Gets the connection string for the current database configuration.
    /// Defaults to sqlite
    /// </summary>
    public static string GetConnectionString(string? dbConnection = null)
    {
        dbConnection ??= GetDbConnection();

        return dbConnection switch
        {
            "mysql" => GetMySqlConnectionString(),
            "postgres" or "postgresql" => GetPostgresConnectionString(),
            _ => GetSqliteConnectionString()
        };
    }

    public static void ConfigureDbContext(DbContextOptionsBuilder options, string? dbConnection = null)
    {
        dbConnection ??= GetDbConnection();

        switch (dbConnection)
        {
            case "mysql":
                ConfigureMySql(options);
                break;
            case "postgres":
            case "postgresql":
                ConfigurePostgres(options);
                break;
            default:
                ConfigureSqlite(options);
                break;
        }
    }

    private static string GetMySqlConnectionString()
    {
        var variables = new Dictionary<string, string>
        {
            { "DB_HOST", Environment.GetEnvironmentVariable("DB_HOST") ?? "Lingarr.Mysql" },
            { "DB_PORT", Environment.GetEnvironmentVariable("DB_PORT") ?? "3306" },
            { "DB_DATABASE", Environment.GetEnvironmentVariable("DB_DATABASE") ?? "Lingarr" },
            { "DB_USERNAME", Environment.GetEnvironmentVariable("DB_USERNAME") ?? "Lingarr" },
            { "DB_PASSWORD", Environment.GetEnvironmentVariable("DB_PASSWORD") ?? throw new InvalidOperationException("DB_PASSWORD environment variable is required for non-SQLite databases") }
        };

        var missingVariables = variables.Where(kv => string.IsNullOrEmpty(kv.Value)).Select(kv => kv.Key).ToList();
        if (missingVariables.Any())
        {
            throw new InvalidOperationException(
                $"MySQL connection environment variable(s) '{string.Join(", ", missingVariables)}' is missing or empty.");
        }

        return $"Server={variables["DB_HOST"]};Port={variables["DB_PORT"]};Database={variables["DB_DATABASE"]};Uid={variables["DB_USERNAME"]};Pwd={variables["DB_PASSWORD"]};Allow User Variables=True";
    }

    private static string GetSqliteConnectionString()
    {
        return $"Data Source={GetSqliteDbPath()};Foreign Keys=True";
    }

    public static string GetSqliteDbPath()
    {
        var configuredPath = Environment.GetEnvironmentVariable("SQLITE_DB_PATH") ?? "local.db";
        return ResolvePath(configuredPath);
    }

    public static string GetHangfireSqlitePath()
    {
        var configuredPath = Environment.GetEnvironmentVariable("DB_HANGFIRE_SQLITE_PATH") ?? "Hangfire.db";
        return ResolvePath(configuredPath);
    }

    public static string GetEncryptionKeysPath()
    {
        var configuredPath = Environment.GetEnvironmentVariable("ENCRYPTION_KEYS");
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return EnsureDirectory(configuredPath);
        }

        return EnsureDirectory(Path.Combine(GetConfigDirectory(), "keys"));
    }

    public static string GetConfigDirectory()
    {
        var configuredDirectory = Environment.GetEnvironmentVariable("CONFIG_DIR");
        if (!string.IsNullOrWhiteSpace(configuredDirectory))
        {
            return EnsureDirectory(configuredDirectory);
        }

        if (Directory.Exists(ContainerConfigPath))
        {
            return ContainerConfigPath;
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            return EnsureDirectory(Path.Combine(localAppData, "Lingarr", "config"));
        }

        return EnsureDirectory(Path.Combine(AppContext.BaseDirectory, "config"));
    }

    private static string ResolvePath(string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath))
        {
            EnsureParentDirectory(configuredPath);
            return configuredPath;
        }

        var configDirectory = GetConfigDirectory();
        var resolvedPath = Path.Combine(configDirectory, configuredPath);
        EnsureParentDirectory(resolvedPath);
        return resolvedPath;
    }

    private static string EnsureDirectory(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }

    private static void EnsureParentDirectory(string filePath)
    {
        var parent = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(parent))
        {
            return;
        }

        Directory.CreateDirectory(parent);
    }

    private static string GetPostgresConnectionString()
    {
        var variables = new Dictionary<string, string>
        {
            { "DB_HOST", Environment.GetEnvironmentVariable("DB_HOST") ?? "Lingarr.Postgres" },
            { "DB_PORT", Environment.GetEnvironmentVariable("DB_PORT") ?? "5432" },
            { "DB_DATABASE", Environment.GetEnvironmentVariable("DB_DATABASE") ?? "Lingarr" },
            { "DB_USERNAME", Environment.GetEnvironmentVariable("DB_USERNAME") ?? "Lingarr" },
            { "DB_PASSWORD", Environment.GetEnvironmentVariable("DB_PASSWORD") ?? throw new InvalidOperationException("DB_PASSWORD environment variable is required for non-SQLite databases") }
        };

        var missingVariables = variables.Where(kv => string.IsNullOrEmpty(kv.Value)).Select(kv => kv.Key).ToList();
        if (missingVariables.Any())
        {
            throw new InvalidOperationException(
                $"PostgreSQL connection environment variable(s) '{string.Join(", ", missingVariables)}' is missing or empty.");
        }

        return $"Host={variables["DB_HOST"]};Port={variables["DB_PORT"]};Database={variables["DB_DATABASE"]};Username={variables["DB_USERNAME"]};Password={variables["DB_PASSWORD"]}";
    }

    /**
     * MySql configuration
     */
    private static void ConfigureMySql(DbContextOptionsBuilder options)
    {
        var connectionString = GetMySqlConnectionString();

        options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString),
                mysqlOptions => mysqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
            .UseSnakeCaseNamingConvention();
    }

    /**
     * Sqlite configuration
     */
    private static void ConfigureSqlite(DbContextOptionsBuilder options)
    {
        var connectionString = GetSqliteConnectionString();

        options.UseSqlite(connectionString,
                sqliteOptions => sqliteOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
            .UseSnakeCaseNamingConvention();
    }

    /**
     * Postgres configuration
     */
    private static void ConfigurePostgres(DbContextOptionsBuilder options)
    {
        var connectionString = GetPostgresConnectionString();

        options.UseNpgsql(connectionString,
                npgsqlOptions => npgsqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
            .UseSnakeCaseNamingConvention();
    }
}
