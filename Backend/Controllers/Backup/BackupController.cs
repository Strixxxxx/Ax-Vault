using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Backend.Data;
using Backend.Models;
using Backend.Models.Backup;
using Backend.Services;

namespace Backend.Controllers.Backup
{
    [ApiController]
    [Route("api/backup")]
    [Authorize]
    public class BackupController : ControllerBase
    {
        private readonly ILogger<BackupController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly PlatformTableService _platformTableService;

        public BackupController(
            ILogger<BackupController> logger,
            ApplicationDbContext context,
            PlatformTableService platformTableService)
        {
            _logger = logger;
            _context = context;
            _platformTableService = platformTableService;
        }

        /// <summary>
        /// EXPORT: Fetches all vault data for the current user.
        /// Returns a structured JSON containing all platforms and their encrypted accounts.
        /// The frontend will then apply a second layer of AES-256-GCM encryption.
        /// </summary>
        [HttpGet("export")]
        public async Task<IActionResult> ExportBackup()
        {
            try
            {
                var user = await GetCurrentUserAsync();
                if (user == null) return Unauthorized(new { Message = "User identity not found." });

                _logger.LogInformation("Backup export requested for AccountID: {accountId}", user.AccountID);

                // Step 1: Get all platforms for this user from the 'Vaults' directory table
                var platformNames = await _context.Vaults
                    .Where(v => v.AccountID == user.AccountID)
                    .Select(v => v.Platform)
                    .Distinct()
                    .ToListAsync();

                var exportModel = new BackupExportModel { Version = 1 };
                string connectionString = ConnectionHelper.GetMasterConnectionString();

                // Step 2: For each platform, fetch the raw (vault-key-encrypted) account rows
                foreach (var platformName in platformNames)
                {
                    string tableName = $"{user.AccountID}_{platformName}";
                    var platformBackup = new PlatformBackupModel { PlatformName = platformName };

                    try
                    {
                        using var connection = new NpgsqlConnection(connectionString);
                        await connection.OpenAsync();

                        string query = $"SELECT \"username\", \"password\", \"description\", \"created_at\" FROM \"{tableName}\"";
                        using var command = new NpgsqlCommand(query, connection);
                        using var reader = await command.ExecuteReaderAsync();

                        while (await reader.ReadAsync())
                        {
                            platformBackup.Accounts.Add(new AccountBackupModel
                            {
                                Username = reader.GetString(0),       // Already vault-key encrypted
                                Password = reader.GetString(1),       // Already vault-key encrypted
                                Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                                CreatedAt = reader.GetDateTime(3)
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not fetch data for table '{tableName}'. Skipping.", tableName);
                    }

                    exportModel.Platforms.Add(platformBackup);
                }

                _logger.LogInformation("Backup export complete. Platforms: {count}", exportModel.Platforms.Count);
                return Ok(exportModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during backup export.");
                return StatusCode(500, new { Message = $"Export failed: {ex.Message}" });
            }
        }

        /// <summary>
        /// IMPORT: Receives the decrypted backup payload from the frontend 
        /// and restores all platforms and accounts.
        /// This uses a "merge" strategy: creates missing platforms and inserts missing accounts.
        /// </summary>
        [HttpPost("import")]
        public async Task<IActionResult> ImportBackup([FromBody] BackupExportModel importData)
        {
            try
            {
                if (importData == null || importData.Platforms == null)
                    return BadRequest(new { Message = "Invalid backup data." });

                var user = await GetCurrentUserAsync();
                if (user == null) return Unauthorized(new { Message = "User identity not found." });

                _logger.LogInformation("Backup import requested for AccountID: {accountId}", user.AccountID);

                string connectionString = ConnectionHelper.GetMasterConnectionString();
                int totalRestored = 0;

                foreach (var platformBackup in importData.Platforms)
                {
                    if (string.IsNullOrWhiteSpace(platformBackup.PlatformName)) continue;

                    string tableName = $"{user.AccountID}_{platformBackup.PlatformName}";

                    // Step 1: Ensure platform table exists (creates if missing)
                    await _platformTableService.CreatePlatformTableAsync(user.AccountID, platformBackup.PlatformName);

                    // Step 2: Ensure a Vault entry exists for this platform
                    bool vaultExists = await _context.Vaults
                        .AnyAsync(v => v.AccountID == user.AccountID && v.Platform == platformBackup.PlatformName);

                    if (!vaultExists)
                    {
                        _context.Vaults.Add(new Vault
                        {
                            AccountID = user.AccountID,
                            Platform = platformBackup.PlatformName
                        });
                        await _context.SaveChangesAsync();
                    }

                    // Step 3: Insert accounts. We insert all accounts from the backup.
                    // The accounts are already vault-key encrypted, so we insert them as-is.
                    if (platformBackup.Accounts == null || !platformBackup.Accounts.Any()) continue;

                    using var connection = new NpgsqlConnection(connectionString);
                    await connection.OpenAsync();

                    foreach (var account in platformBackup.Accounts)
                    {
                        string insertQuery = $@"
                            INSERT INTO ""{tableName}"" (""username"", ""password"", ""description"", ""created_at"")
                            VALUES (@Username, @Password, @Description, @CreatedAt)";

                        using var command = new NpgsqlCommand(insertQuery, connection);
                        command.Parameters.AddWithValue("@Username", account.Username);
                        command.Parameters.AddWithValue("@Password", account.Password);
                        command.Parameters.AddWithValue("@Description", (object?)account.Description ?? DBNull.Value);
                        command.Parameters.AddWithValue("@CreatedAt", account.CreatedAt);

                        await command.ExecuteNonQueryAsync();
                        totalRestored++;
                    }

                    _logger.LogInformation("Restored {count} accounts for platform '{platform}'.", platformBackup.Accounts.Count, platformBackup.PlatformName);
                }

                _logger.LogInformation("Backup import complete. Total accounts restored: {totalRestored}", totalRestored);
                return Ok(new { Message = $"Restore complete. {totalRestored} account(s) restored across {importData.Platforms.Count} platform(s)." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during backup import.");
                return StatusCode(500, new { Message = $"Import failed: {ex.Message}" });
            }
        }

        private async Task<User?> GetCurrentUserAsync()
        {
            var usernameClaim = User.Identity?.Name;
            if (string.IsNullOrEmpty(usernameClaim))
                usernameClaim = User.Claims.FirstOrDefault(c => c.Type == "sub" || c.Type == JwtRegisteredClaimNames.Sub || c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(usernameClaim))
                usernameClaim = User.Claims.FirstOrDefault(c => c.Type == "unique_name" || c.Type == JwtRegisteredClaimNames.UniqueName || c.Type == ClaimTypes.Name)?.Value;

            if (string.IsNullOrEmpty(usernameClaim))
            {
                _logger.LogWarning("BackupController: No identity claim found in token.");
                return null;
            }

            var fixedSalt = PasswordHasher.GetDeterministicSalt();
            var inputHash = PasswordHasher.HashDeterministic(usernameClaim.ToLowerInvariant(), fixedSalt);
            return await _context.Users.FirstOrDefaultAsync(u => u.UsernameHashed == inputHash || u.EmailHashed == inputHash);
        }
    }
}
