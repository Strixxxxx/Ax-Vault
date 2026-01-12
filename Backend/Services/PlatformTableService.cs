using Microsoft.Data.SqlClient;
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
        string safePlatform = SanitizePlatformName(platform);
        string tableName = $"{accountId}_{safePlatform}";

        _logger.LogInformation($"Ensuring table '{tableName}' exists.");

        string createTableSql = $@"
            IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '{tableName}')
            BEGIN
                CREATE TABLE [{tableName}] (
                    Id BIGINT PRIMARY KEY IDENTITY(1,1),
                    Username NVARCHAR(MAX), -- Encrypted
                    Password NVARCHAR(MAX), -- Encrypted
                    Description NVARCHAR(MAX),
                    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
                    UpdatedAt DATETIME2 NULL
                );
            END";

        try
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand(createTableSql, connection))
                {
                    await command.ExecuteNonQueryAsync();
                }
            }
            _logger.LogInformation($"Table '{tableName}' verified/created.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to create table '{tableName}'");
            throw;
        }
    }

    private string SanitizePlatformName(string platform)
    {
        // Allow alphanumerics only
        return new string(platform.Where(char.IsLetterOrDigit).ToArray());
    }
}
