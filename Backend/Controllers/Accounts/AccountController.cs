using Microsoft.AspNetCore.Mvc;
using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using Backend.Services; // Add the services namespace

namespace Backend.Controllers.Accounts
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccountController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public AccountController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpPost]
        public async Task<IActionResult> CreateAccount([FromBody] AccountModel model, [FromHeader(Name = "X-Username")] string username)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Use the username from header instead of claims
                if (string.IsNullOrEmpty(username))
                {
                    return Unauthorized("Username not provided.");
                }

                // Get the database name for the user from the Accounts table in master database using username
                string? dbName = await GetUserDatabaseNameByUsername(username);
                if (string.IsNullOrEmpty(dbName))
                {
                    return NotFound("User database not found.");
                }

                // Create the table in the user's database if it doesn't exist
                await EnsureAccountTableExists(dbName);

                // Insert the account into the table
                bool result = await InsertAccount(dbName, model);
                if (!result)
                {
                    return StatusCode(500, "Failed to create account.");
                }

                return Ok(new { message = "Account created successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        private async Task<string?> GetUserDatabaseNameByUsername(string username)
        {
            // Use the centralized ConnectionHelper to get the master database connection string
            string connectionString = ConnectionHelper.GetMasterConnectionString();
            string? dbName = null;

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                
                // Changed query to search by username instead of user_id
                string query = "SELECT [Database Name] FROM Accounts WHERE username = @Username";
                
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Username", username);
                    var result = await command.ExecuteScalarAsync();
                    
                    dbName = result?.ToString();
                }
            }

            return dbName;
        }

        private async Task EnsureAccountTableExists(string dbName)
        {
            // Use the centralized ConnectionHelper to get the user-specific database connection string
            string connectionString = ConnectionHelper.GetUserDbConnectionString(dbName);

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                
                string query = @"
                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Accounts')
                    BEGIN
                        CREATE TABLE Accounts (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            platform NVARCHAR(50) NOT NULL,
                            username NVARCHAR(50) NOT NULL,
                            password NVARCHAR(255) NOT NULL,
                            description NVARCHAR(MAX),
                            created_at DATETIME DEFAULT GETDATE()
                        )
                    END";
                
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task<bool> InsertAccount(string dbName, AccountModel model)
        {
            // Use the centralized ConnectionHelper to get the user-specific database connection string
            string connectionString = ConnectionHelper.GetUserDbConnectionString(dbName);

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                
                string query = @"
                    INSERT INTO Accounts (platform, username, password, description, created_at)
                    VALUES (@Platform, @Username, @Password, @Description, @CreatedAt)";
                
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Platform", model.Platform);
                    command.Parameters.AddWithValue("@Username", model.Username);
                    command.Parameters.AddWithValue("@Password", model.Password);
                    command.Parameters.AddWithValue("@Description", (object)model.Description ?? DBNull.Value);
                    command.Parameters.AddWithValue("@CreatedAt", DateTime.Now); // Use current time instead of timezone conversion
                    
                    int rowsAffected = await command.ExecuteNonQueryAsync();
                    return rowsAffected > 0;
                }
            }
        }
    }

    public class AccountModel
    {
        [Required]
        [StringLength(50)]
        public string Platform { get; set; } = string.Empty;
        
        [Required]
        [StringLength(50)]
        public string Username { get; set; } = string.Empty;
        
        [Required]
        [StringLength(255)]
        public string Password { get; set; } = string.Empty;
        
        public string Description { get; set; } = string.Empty;
        
        public DateTime? CreatedAt { get; set; }
    }
}
 