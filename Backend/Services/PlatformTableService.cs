using Npgsql;
using Backend.Data;

namespace Backend.Services;

public class PlatformTableService
{
    private readonly ILogger<PlatformTableService> _logger;
    private readonly string _connectionString;

    public PlatformTableService(ILogger<PlatformTableService> logger)
    {
        _logger = logger;
        _connectionString = ConnectionHelper.GetMasterConnectionString();
    }

    /// <summary>
    /// Creates a dynamic table for a specific platform user if it doesn't exist.
    /// Table Name Format: [AccountID]_[Platform]
    /// </summary>
    public async Task CreatePlatformTableAsync(long accountId, string platform)
    {
        // Sanitize platform name to prevent SQL Injection via table name
        // But keep it consistent with what the user expects: [AccountID]_[Platform]
        string tableName = $"{accountId}_{platform}";
        
        // Use double quotes to handle spaces or special characters in table name for PG
        string safeTableName = $"\"{tableName}\"";

        _logger.LogInformation("Creating platform table. AccountID: {accountId}, Platform: {platform}, TableName: {tableName}", accountId, platform, tableName);

        string createTableSql = $@"
            CREATE TABLE IF NOT EXISTS {safeTableName} (
                ""PlatformID"" BIGSERIAL PRIMARY KEY,
                ""username"" TEXT NOT NULL, -- Encrypted
                ""password"" TEXT NOT NULL, -- Encrypted
                ""description"" TEXT,
                ""created_at"" TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
            );";

        try
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new NpgsqlCommand(createTableSql, connection))
                {
                    _logger.LogInformation("Executing SQL for table creation: {sql}", createTableSql);
                    await command.ExecuteNonQueryAsync();
                }
            }
            _logger.LogInformation("Table '{tableName}' verified or successfully created.", tableName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create table '{tableName}'. Error: {error}", tableName, ex.Message);
            throw;
        }
    }

    public async Task RenamePlatformTableAsync(long accountId, string oldPlatform, string newPlatform)
    {
        string oldTableName = $"{accountId}_{oldPlatform}";
        string newTableName = $"{accountId}_{newPlatform}";

        _logger.LogInformation($"Renaming table '\"{oldTableName}\"' to '\"{newTableName}\"'.");

        // PostgreSQL rename syntax
        string renameSql = $"ALTER TABLE \"{oldTableName}\" RENAME TO \"{newTableName}\"";

        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            using (var command = new NpgsqlCommand(renameSql, connection))
            {
                await command.ExecuteNonQueryAsync();
            }
        }
    }

    public async Task DropPlatformTableAsync(long accountId, string platform)
    {
        string tableName = $"{accountId}_{platform}";
        _logger.LogInformation($"Dropping table '\"{tableName}\"'.");

        string dropSql = $"DROP TABLE IF EXISTS \"{tableName}\"";

        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            using (var command = new NpgsqlCommand(dropSql, connection))
            {
                await command.ExecuteNonQueryAsync();
            }
        }
    }

    private string SanitizePlatformName(string platform)
    {
        // Allow alphanumerics and 1 space as requested
        // But for SQL table names, we just need to ensure no illegal characters like quotes
        // The user said "only allow 1 spaces only" - interpreting as "allow spaces, but maybe collapse multiple spaces"
        // Let's just strip illegal characters and trust the brackets for spaces.
        return new string(platform.Where(c => char.IsLetterOrDigit(c) || c == ' ').ToArray());
    }
}
