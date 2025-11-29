using Microsoft.AspNetCore.Mvc;
using System;
using System.ComponentModel.DataAnnotations;
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using System.Collections.Generic;
using Backend.Services; // Add the services namespace

namespace Backend.Controllers.Accounts
{
    [ApiController]
    [Route("api/platform")]
    public class PlatformController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<PlatformController> _logger;

        public PlatformController(IConfiguration configuration, ILogger<PlatformController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> CreatePlatform(
            [FromBody] PlatformModel model, 
            [FromHeader(Name = "X-Username")] string username,
            [FromHeader(Name = "X-Database-Name")] string? databaseName = null)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                if (string.IsNullOrEmpty(username))
                {
                    return Unauthorized("Username not provided.");
                }

                string? dbName = databaseName;
                if (string.IsNullOrEmpty(dbName))
                {
                    _logger.LogInformation("Database name not provided in header, looking up for user {Username}", username);
                    dbName = await GetUserDatabaseNameByUsername(username);
                    if (string.IsNullOrEmpty(dbName))
                    {
                        return NotFound("User database not found.");
                    }
                }
                
                _logger.LogInformation("Using database '{dbName}' for user '{Username}'", dbName, username);

                // Use the centralized connection helper
                string connectionString = ConnectionHelper.GetUserDbConnectionString(dbName);
                _logger.LogInformation("Using connection string (masked): {maskedString}", ConnectionHelper.MaskConnectionString(connectionString));

                // Test the connection before proceeding
                await TestDatabaseConnection(connectionString);

                // Create the platform-specific table in the user's database if it doesn't exist
                await EnsurePlatformTableExists(dbName, model.Name);
                _logger.LogInformation("Successfully ensured platform-specific table '{PlatformName}' exists in database '{dbName}'", model.Name, dbName);

                // Insert the platform metadata into the 'Platforms' table
                bool result = await InsertPlatformMetadata(dbName, model.Name);
                if (!result)
                {
                    _logger.LogError("Platform metadata insert failed for '{PlatformName}' in db '{dbName}': No rows affected", model.Name, dbName);
                    return StatusCode(500, "Failed to create platform record.");
                }

                _logger.LogInformation("Successfully inserted platform metadata for '{PlatformName}' in db '{dbName}'", model.Name, dbName);
                return Ok(new { message = "Platform created successfully." });
            }
            catch (SqlException sqlEx)
            {
                _logger.LogError(sqlEx, "A SQL error occurred while creating a platform for user {Username}", username);
                return StatusCode(500, $"A database error occurred: {sqlEx.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while creating a platform for user {Username}", username);
                return StatusCode(500, $"An internal server error occurred: {ex.Message}");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetPlatforms(
            [FromHeader(Name = "X-Username")] string username,
            [FromHeader(Name = "X-Database-Name")] string? databaseName = null)
        {
            try
            {
                if (string.IsNullOrEmpty(username))
                {
                    return Unauthorized("Username not provided.");
                }

                string? dbName = databaseName;
                if (string.IsNullOrEmpty(dbName))
                {
                    _logger.LogInformation("Database name not provided in header for GetPlatforms, looking up for user {Username}", username);
                    dbName = await GetUserDatabaseNameByUsername(username);
                    if (string.IsNullOrEmpty(dbName))
                    {
                        return NotFound("User database not found.");
                    }
                }
                _logger.LogInformation("Getting platforms from database '{dbName}' for user '{Username}'", dbName, username);

                // Use the centralized connection helper
                string connectionString = ConnectionHelper.GetUserDbConnectionString(dbName);
                _logger.LogInformation("Using connection string (masked): {maskedString}", ConnectionHelper.MaskConnectionString(connectionString));

                var platforms = new List<PlatformApiModel>();
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    await EnsurePlatformsMetadataTableExists(connection);

                    string query = "SELECT name FROM Platforms ORDER BY name";
                    using (var command = new SqlCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                string platformName = reader.GetString(0);
                                int accountCount = await GetAccountCountForPlatform(connectionString, platformName);
                                platforms.Add(new PlatformApiModel { Name = platformName, AccountCount = accountCount });
                            }
                        }
                    }
                }

                return Ok(platforms);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred in GetPlatforms for user {Username}", username);
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
        
        private async Task<string?> GetUserDatabaseNameByUsername(string username)
        {
            string connectionString = ConnectionHelper.GetMasterConnectionString();
            string? dbName = null;

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                string query = "SELECT [Database Name] FROM Accounts WHERE username = @Username";
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Username", username);
                    var result = await command.ExecuteScalarAsync();
                    dbName = result?.ToString();
                }
            }
            return dbName;
        }
        
        private async Task TestDatabaseConnection(string connectionString)
        {
            using var testConnection = new SqlConnection(connectionString);
            await testConnection.OpenAsync();
        }

        private async Task EnsurePlatformsMetadataTableExists(SqlConnection connection)
        {
            string createPlatformsTable = @"
                IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Platforms')
                BEGIN
                    CREATE TABLE Platforms (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        name NVARCHAR(50) NOT NULL UNIQUE,
                        created_at DATETIME DEFAULT GETDATE()
                    )
                END";
            using var createCmd = new SqlCommand(createPlatformsTable, connection);
            await createCmd.ExecuteNonQueryAsync();
        }

        private async Task EnsurePlatformTableExists(string dbName, string platformName)
        {
            string connectionString = ConnectionHelper.GetUserDbConnectionString(dbName);
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            string query = @"
                IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @PlatformName)
                BEGIN
                    DECLARE @CreateTableSQL NVARCHAR(MAX);
                    SET @CreateTableSQL = N'CREATE TABLE [' + @PlatformName + N'] (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        username NVARCHAR(50) NOT NULL,
                        password NVARCHAR(255) NOT NULL,
                        description NVARCHAR(MAX),
                        created_at DATETIME DEFAULT GETDATE()
                    )';
                    EXEC sp_executesql @CreateTableSQL;
                END";
            
            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@PlatformName", platformName);
            await command.ExecuteNonQueryAsync();
        }

        private async Task<bool> InsertPlatformMetadata(string dbName, string platformName)
        {
            string connectionString = ConnectionHelper.GetUserDbConnectionString(dbName);
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            await EnsurePlatformsMetadataTableExists(connection);
            
            string query = @"
                IF NOT EXISTS (SELECT 1 FROM Platforms WHERE name = @Name)
                BEGIN
                    INSERT INTO Platforms (name, created_at)
                    VALUES (@Name, @CreatedAt)
                END";

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@Name", platformName);
            command.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);
            
            int rowsAffected = await command.ExecuteNonQueryAsync();
            // If the platform already exists, rowsAffected will be 0, but we consider it a success.
            // A negative value would indicate an error.
            return rowsAffected >= 0;
        }

        private async Task<int> GetAccountCountForPlatform(string connectionString, string platformName)
        {
            using var countConn = new SqlConnection(connectionString);
            await countConn.OpenAsync();
            using var countCmd = new SqlCommand($"IF OBJECT_ID(N'[{platformName}]', N'U') IS NOT NULL SELECT COUNT(*) FROM [{platformName}] ELSE SELECT 0", countConn);
            return Convert.ToInt32(await countCmd.ExecuteScalarAsync());
        }
    }

    public class PlatformModel
    {
        [Required]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;
        
        public DateTime? CreatedAt { get; set; }
    }

    public class PlatformApiModel
    {
        public string Name { get; set; } = string.Empty;
        public int AccountCount { get; set; }
    }
}
 