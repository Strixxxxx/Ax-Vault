using Microsoft.Data.SqlClient;
using System.Text;

namespace Backend.Services;

public class UserDatabaseService
{
    private readonly ILogger<UserDatabaseService> _logger;
    private readonly string _masterConnectionString;

    public UserDatabaseService(ILogger<UserDatabaseService> logger)
    {
        _logger = logger;
        
        try
        {
            // Use ConnectionHelper to get a connection string to the master SQL Server instance (without a specific database)
            _masterConnectionString = ConnectionHelper.GetMasterConnectionStringWithoutInitialCatalog();
            
            var maskedConnectionString = ConnectionHelper.MaskConnectionString(_masterConnectionString);
            _logger.LogInformation("UserDatabaseService initialized with connection string: {maskedConnectionString}", maskedConnectionString);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to initialize UserDatabaseService due to missing environment variables.");
            throw;
        }
    }

    // Overload that generates a database name based on username
    public async Task<string> CreateUserDatabaseAsync(string username)
    {
        // Sanitize username for database name (remove special characters, spaces, etc.)
        var sanitizedUsername = SanitizeForDatabaseName(username);
        var databaseName = $"{sanitizedUsername}_database";
        
        return await CreateUserDatabaseAsync(username, databaseName);
    }
    
    // Overload that accepts a custom database name
    public async Task<string> CreateUserDatabaseAsync(string username, string databaseName)
    {
        try
        {
            _logger.LogInformation($"Creating database '{databaseName}' for user '{username}'");
            
            using (var connection = new SqlConnection(_masterConnectionString))
            {
                await connection.OpenAsync();
                
                // Check if database already exists
                var checkCommand = new SqlCommand(
                    "SELECT COUNT(*) FROM sys.databases WHERE name = @dbName", 
                    connection);
                checkCommand.Parameters.AddWithValue("@dbName", databaseName);
                
                var result = await checkCommand.ExecuteScalarAsync();
                var exists = result != null && (int)result > 0;
                
                if (exists)
                {
                    _logger.LogWarning($"Database '{databaseName}' already exists, using existing database");
                    // Even if database exists, make sure permissions are set correctly
                    await EnsureDatabasePermissionsAsync(databaseName);
                    return databaseName;
                }
                
                // Create the database - Properly escape the database name with square brackets
                var createCommand = new SqlCommand(
                    $"CREATE DATABASE [{databaseName}]", 
                    connection);
                
                await createCommand.ExecuteNonQueryAsync();
                _logger.LogInformation($"Database '{databaseName}' created successfully");
                
                // Set up permissions for the new database
                await EnsureDatabasePermissionsAsync(databaseName);
                
                return databaseName;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error creating database for user '{username}'");
            throw new InvalidOperationException($"Failed to create user database: {ex.Message}", ex);
        }
    }
    
    private string SanitizeForDatabaseName(string input)
    {
        // Remove any characters that aren't allowed in SQL Server database names
        var sb = new StringBuilder();
        foreach (char c in input)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                sb.Append(c);
            }
        }
        
        // Ensure it starts with a letter or underscore
        var sanitized = sb.ToString();
        if (sanitized.Length > 0 && !char.IsLetter(sanitized[0]) && sanitized[0] != '_')
        {
            sanitized = "db_" + sanitized;
        }
        
        // If the sanitized name is empty, use a default
        if (string.IsNullOrEmpty(sanitized))
        {
            sanitized = "user_db";
        }
        
        return sanitized;
    }
    
    public async Task EnsureDatabasePermissionsAsync(string databaseName)
    {
        try
        {
            _logger.LogInformation($"Setting up permissions for database '{databaseName}'");
            
            // Get the SQL login from environment
            var sqlLogin = Environment.GetEnvironmentVariable("DB_USER");
            
            // First connect to master database
            using (var connection = new SqlConnection(_masterConnectionString))
            {
                await connection.OpenAsync();
                
                // This is a more robust way to ensure the login has ownership of the database.
                // The login that creates the database is automatically the owner (dbo), but if the DB
                // already existed, this ensures the correct login is the owner.
                string setupUserSql = $"ALTER AUTHORIZATION ON DATABASE::[{databaseName}] TO [{sqlLogin}]";

                var command = new SqlCommand(setupUserSql, connection);
                await command.ExecuteNonQueryAsync();
                
                _logger.LogInformation($"Successfully ensured owner permissions for database '{databaseName}'");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error setting up permissions for database '{databaseName}'");
            throw new InvalidOperationException($"Failed to set up database permissions: {ex.Message}", ex);
        }
    }
}
 