using Microsoft.AspNetCore.Mvc;
using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using Backend.Services;
using Backend.Data;
using Backend.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;

namespace Backend.Controllers.Accounts
{
    [ApiController]
    [Route("api/accounts")]
    [Authorize]
    public class AccountController : ControllerBase
    {
        private readonly ILogger<AccountController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly EncryptionService _encryptionService;
        private readonly PlatformTableService _platformTableService;

        public AccountController(
            ILogger<AccountController> logger, 
            ApplicationDbContext context,
            PlatformTableService platformTableService,
            EncryptionService encryptionService)
        {
            _logger = logger;
            _context = context;
            _platformTableService = platformTableService;
            _encryptionService = encryptionService;
        }

        private async Task<User?> GetCurrentUserAsync()
        {
            // Try different claim types to be robust
            var usernameClaim = User.Identity?.Name;
            if (string.IsNullOrEmpty(usernameClaim))
            {
                usernameClaim = User.Claims.FirstOrDefault(c => c.Type == "sub" || c.Type == JwtRegisteredClaimNames.Sub || c.Type == ClaimTypes.NameIdentifier)?.Value;
            }
            if (string.IsNullOrEmpty(usernameClaim))
            {
                usernameClaim = User.Claims.FirstOrDefault(c => c.Type == "unique_name" || c.Type == JwtRegisteredClaimNames.UniqueName || c.Type == ClaimTypes.Name)?.Value;
            }

            if (string.IsNullOrEmpty(usernameClaim)) 
            {
                _logger.LogWarning("AccountController: No identity claim found in token.");
                _logger.LogWarning("AccountController: Listing all available claims for debugging:");
                foreach (var claim in User.Claims)
                {
                    _logger.LogWarning("AccountController: Claim: Type={Type}, Value={Value}", claim.Type, claim.Value);
                }
                return null;
            }

            var fixedSalt = PasswordHasher.GetDeterministicSalt();
            var inputHash = PasswordHasher.HashDeterministic(usernameClaim.ToLowerInvariant(), fixedSalt);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UsernameHashed == inputHash || u.EmailHashed == inputHash);
            if (user == null)
            {
                _logger.LogWarning("AccountController: No user found for identity {usernameClaim}", usernameClaim);
            }

            return user;
        }

        [HttpPost("list")]
        public async Task<IActionResult> ListAccounts([FromBody] AccountListRequest request)
        {
            try
            {
                var user = await GetCurrentUserAsync();
                if (user == null) return Unauthorized(new { Message = "User identity not found." });

                // Derive key to decrypt usernames
                var salt = PasswordHasher.GetDeterministicSalt();
                var vaultKey = PasswordHasher.DeriveKeyFromVaultPassword(request.VaultPassword, salt);

                string tableName = $"{user.AccountID}_{request.Platform}";
                var accounts = new List<AccountResponseModel>();

                using (var connection = new SqlConnection(ConnectionHelper.GetMasterConnectionString()))
                {
                    await connection.OpenAsync();
                    string query = $"SELECT PlatformID, username, password, description, created_at FROM [{tableName}]";
                    
                    using var command = new SqlCommand(query, connection);
                    using var reader = await command.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        string encryptedUsername = reader.GetString(1);
                        string decryptedUsername = _encryptionService.Decrypt(encryptedUsername, vaultKey);

                        accounts.Add(new AccountResponseModel
                        {
                            Id = reader.GetInt64(0),
                            Username = decryptedUsername,
                            Password = reader.GetString(2), // Keep encrypted
                            Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                            CreatedAt = reader.GetDateTime(4),
                            TimeZoneId = user.Timezone // Include the user's timezone
                        });
                    }
                }

                return Ok(accounts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing accounts");
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        [HttpPost("add")]
        public async Task<IActionResult> AddAccount([FromBody] AddAccountRequest request)
        {
            try
            {
                var user = await GetCurrentUserAsync();
                if (user == null) return Unauthorized(new { Message = "User identity not found." });

                var salt = PasswordHasher.GetDeterministicSalt();
                var vaultKey = PasswordHasher.DeriveKeyFromVaultPassword(request.VaultPassword, salt);

                string encryptedUsername = _encryptionService.Encrypt(request.Username, vaultKey);
                string encryptedPassword = _encryptionService.Encrypt(request.Password, vaultKey);

                string tableName = $"{user.AccountID}_{request.Platform}";
                
                string query = $@"
                    INSERT INTO [{tableName}] (username, password, description, created_at)
                    VALUES (@Username, @Password, @Description, GETUTCDATE())";

                using (var connection = new SqlConnection(ConnectionHelper.GetMasterConnectionString()))
                {
                    await connection.OpenAsync();
                    using var command = new SqlCommand(query, connection);
                    command.Parameters.AddWithValue("@Username", encryptedUsername);
                    command.Parameters.AddWithValue("@Password", encryptedPassword);
                    command.Parameters.AddWithValue("@Description", (object?)request.Description ?? DBNull.Value);

                    await command.ExecuteNonQueryAsync();
                }

                return Ok(new { Message = "Account added to platform." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding account");
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        [HttpPut("edit")]
        public async Task<IActionResult> EditAccount([FromBody] EditAccountRequest request)
        {
            try
            {
                var user = await GetCurrentUserAsync();
                if (user == null) return Unauthorized(new { Message = "User identity not found." });

                var salt = PasswordHasher.GetDeterministicSalt();
                var vaultKey = PasswordHasher.DeriveKeyFromVaultPassword(request.VaultPassword, salt);

                string encryptedUsername = _encryptionService.Encrypt(request.Username, vaultKey);
                string encryptedPassword = _encryptionService.Encrypt(request.Password, vaultKey);

                string tableName = $"{user.AccountID}_{request.Platform}";
                
                string query = $@"
                    UPDATE [{tableName}] 
                    SET username = @Username, password = @Password, description = @Description
                    WHERE PlatformID = @Id";

                using (var connection = new SqlConnection(ConnectionHelper.GetMasterConnectionString()))
                {
                    await connection.OpenAsync();
                    using var command = new SqlCommand(query, connection);
                    command.Parameters.AddWithValue("@Id", request.Id);
                    command.Parameters.AddWithValue("@Username", encryptedUsername);
                    command.Parameters.AddWithValue("@Password", encryptedPassword);
                    command.Parameters.AddWithValue("@Description", (object?)request.Description ?? DBNull.Value);

                    int affected = await command.ExecuteNonQueryAsync();
                    if (affected == 0) return NotFound("Account row not found.");
                }

                return Ok(new { Message = "Account updated." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing account");
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        [HttpPost("delete")]
        public async Task<IActionResult> DeleteAccount([FromBody] DeleteAccountRequest request)
        {
            try
            {
                var user = await GetCurrentUserAsync();
                if (user == null) return Unauthorized(new { Message = "User identity not found." });

                string tableName = $"{user.AccountID}_{request.Platform}";
                string query = $"DELETE FROM [{tableName}] WHERE PlatformID = @Id";

                using (var connection = new SqlConnection(ConnectionHelper.GetMasterConnectionString()))
                {
                    await connection.OpenAsync();
                    using var command = new SqlCommand(query, connection);
                    command.Parameters.AddWithValue("@Id", request.Id);

                    int affected = await command.ExecuteNonQueryAsync();
                    if (affected == 0) return NotFound("Account row not found.");
                }

                return Ok(new { Message = "Account deleted." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting account");
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        [HttpPost("decrypt-password")]
        public async Task<IActionResult> DecryptPassword([FromBody] DecryptPasswordRequest request)
        {
            try
            {
                var user = await GetCurrentUserAsync();
                if (user == null) return Unauthorized(new { Message = "User identity not found." });

                var salt = PasswordHasher.GetDeterministicSalt();
                var vaultKey = PasswordHasher.DeriveKeyFromVaultPassword(request.VaultPassword, salt);

                string tableName = $"{user.AccountID}_{request.Platform}";
                string query = $"SELECT password FROM [{tableName}] WHERE PlatformID = @Id";

                using (var connection = new SqlConnection(ConnectionHelper.GetMasterConnectionString()))
                {
                    await connection.OpenAsync();
                    using var command = new SqlCommand(query, connection);
                    command.Parameters.AddWithValue("@Id", request.Id);

                    var encryptedPassword = await command.ExecuteScalarAsync() as string;
                    if (encryptedPassword == null) return NotFound("Account row not found.");

                    string decryptedPassword = _encryptionService.Decrypt(encryptedPassword, vaultKey);
                    return Ok(new { Password = decryptedPassword });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error decrypting password");
                return StatusCode(500, new { Message = "Decryption failed. Please check your vault password." });
            }
        }

    }

    public class AccountListRequest
    {
        public string Platform { get; set; } = string.Empty;
        public string VaultPassword { get; set; } = string.Empty;
    }

    public class AddAccountRequest
    {
        public string Platform { get; set; } = string.Empty;
        public string VaultPassword { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class EditAccountRequest : AddAccountRequest
    {
        public long Id { get; set; }
    }

    public class DeleteAccountRequest
    {
        public string Platform { get; set; } = string.Empty;
        public long Id { get; set; }
    }

    public class DecryptPasswordRequest
    {
        public string Platform { get; set; } = string.Empty;
        public long Id { get; set; }
        public string VaultPassword { get; set; } = string.Empty;
    }

    public class AccountResponseModel
    {
        public long Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? TimeZoneId { get; set; } // Added
    }
}
