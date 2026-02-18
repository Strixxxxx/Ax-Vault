using Microsoft.AspNetCore.Mvc;
using System;
using System.ComponentModel.DataAnnotations;
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using System.Collections.Generic;
using Backend.Services;
using Backend.Data;
using Backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Linq;

namespace Backend.Controllers.Accounts
{
    [ApiController]
    [Route("api/platforms")]
    [Authorize]
    public class PlatformController : ControllerBase
    {
        private readonly ILogger<PlatformController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly PlatformTableService _platformTableService;

        public PlatformController(
            ILogger<PlatformController> logger, 
            ApplicationDbContext context,
            PlatformTableService platformTableService)
        {
            _logger = logger;
            _context = context;
            _platformTableService = platformTableService;
        }

        /*
         * LOGIC: GET PLATFORMS
         * 1. Get User AccountID from Token.
         * 2. Query 'Vaults' table for all Platforms linked to this AccountID.
         * 3. For each Platform, count the records in its dynamic table ([AccountID]_[Platform]).
         */
        [HttpGet]
        public async Task<IActionResult> GetPlatforms()
        {
            try
            {
                var user = await GetCurrentUserAsync();
                if (user == null)
                {
                    _logger.LogWarning("GetPlatforms: Authorization failed - active user not identified or missing from DB.");
                    return Unauthorized(new { Message = "User account not identified." });
                }

                long accountId = user.AccountID;
                _logger.LogInformation("Retrieving platforms for AccountID: {accountId}", accountId);

                var platforms = new List<PlatformApiModel>();
                
                // Get platforms from the 'Vaults' table
                var platformNames = await _context.Vaults
                    .Where(v => v.AccountID == accountId)
                    .Select(v => v.Platform)
                    .Distinct() // Ensure unique platform names
                    .ToListAsync();

                string connectionString = ConnectionHelper.GetMasterConnectionString();

                foreach (var platformName in platformNames)
                {
                    // Use the exact platform name for table construction, but handle potential naming variations
                    // The user wants [AccountID]_[Platform]
                    string tableName = $"{accountId}_{platformName}";
                    
                    _logger.LogInformation("Checking account count for platform: {platformName}, table: {tableName}", platformName, tableName);
                    int accountCount = await GetAccountCountForPlatform(connectionString, tableName);
                    platforms.Add(new PlatformApiModel { Name = platformName, AccountCount = accountCount });
                }

                return Ok(platforms);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving platforms");
                return StatusCode(500, new { Message = $"Internal server error: {ex.Message}" });
            }
        }
        
        [HttpPost("add")]
        public async Task<IActionResult> AddPlatform([FromBody] string name)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name)) return BadRequest(new { Message = "Platform name is required." });

                _logger.LogInformation("AddPlatform called for Name: {name}", name);

                var user = await GetCurrentUserAsync();
                if (user == null) 
                {
                    _logger.LogWarning("AddPlatform: User not found or unauthorized.");
                    return Unauthorized(new { Message = "User identity not found." });
                }

                _logger.LogInformation("AddPlatform: User identified as AccountID: {accountId}", user.AccountID);

                // 1. Create table [ID]_[Name]
                await _platformTableService.CreatePlatformTableAsync(user.AccountID, name);
                _logger.LogInformation("AddPlatform: Dynamic table creation requested for {accountId}_{name}", user.AccountID, name);

                // 2. Add to Vaults directory
                var vault = new Vault
                {
                    AccountID = user.AccountID,
                    Platform = name
                };
                _context.Vaults.Add(vault);
                await _context.SaveChangesAsync();
                _logger.LogInformation("AddPlatform: Platform '{name}' added to Vaults table for AccountID {accountId}", name, user.AccountID);

                return Ok(new { Message = $"Platform '{name}' added successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding platform");
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        [HttpPut("edit")]
        public async Task<IActionResult> EditPlatform([FromBody] EditPlatformRequest request)
        {
            try
            {
                _logger.LogInformation("EditPlatform called for OldName: {oldName}, NewName: {newName}", request.OldName, request.NewName);

                var user = await GetCurrentUserAsync();
                if (user == null) return Unauthorized(new { Message = "User identity not found." });

                // 1. Find the vault entry
                var vault = await _context.Vaults.FirstOrDefaultAsync(v => v.AccountID == user.AccountID && v.Platform == request.OldName);
                if (vault == null) 
                {
                    _logger.LogWarning("EditPlatform: Platform '{oldName}' not found for AccountID {accountId}", request.OldName, user.AccountID);
                    return NotFound("Platform not found.");
                }

                // 2. Rename table
                _logger.LogInformation("EditPlatform: Renaming dynamic table for {accountId}", user.AccountID);
                await _platformTableService.RenamePlatformTableAsync(user.AccountID, request.OldName, request.NewName);

                // 3. Update Vaults table
                vault.Platform = request.NewName;
                await _context.SaveChangesAsync();
                _logger.LogInformation("EditPlatform: Platform renamed to '{newName}' in Vaults table.", request.NewName);

                return Ok(new { Message = $"Platform renamed to '{request.NewName}'." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing platform");
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        [HttpPost("delete")]
        public async Task<IActionResult> DeletePlatform([FromBody] DeletePlatformRequest request)
        {
            try
            {
                _logger.LogInformation("DeletePlatform called for Name: {name}", request.Name);

                var user = await GetCurrentUserAsync();
                if (user == null) return Unauthorized(new { Message = "User identity not found." });

                // Verification: Name must match
                if (request.ConfirmName != request.Name)
                {
                    _logger.LogWarning("DeletePlatform: Confirmation name mismatch. Expected: {expected}, Got: {got}", request.Name, request.ConfirmName);
                    return BadRequest("Platform name confirmation does not match.");
                }

                // 1. Find vault entry
                var vault = await _context.Vaults.FirstOrDefaultAsync(v => v.AccountID == user.AccountID && v.Platform == request.Name);
                if (vault == null) 
                {
                    _logger.LogWarning("DeletePlatform: Platform '{name}' not found for AccountID {accountId}", request.Name, user.AccountID);
                    return NotFound("Platform not found.");
                }

                // 2. Drop table
                _logger.LogInformation("DeletePlatform: Dropping dynamic table for {accountId}_{name}", user.AccountID, request.Name);
                await _platformTableService.DropPlatformTableAsync(user.AccountID, request.Name);

                // 3. Remove vault entry
                _context.Vaults.Remove(vault);
                await _context.SaveChangesAsync();
                _logger.LogInformation("DeletePlatform: Platform '{name}' removed from Vaults table.", request.Name);

                return Ok(new { Message = $"Platform '{request.Name}' deleted." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting platform");
                return StatusCode(500, new { Message = ex.Message });
            }
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
                _logger.LogWarning("PlatformController: ERROR - No identity claim found in token.");
                _logger.LogWarning("PlatformController: Dumping all claims for diagnostic:");
                foreach (var claim in User.Claims)
                {
                    _logger.LogWarning("Claim: Type={Type}, Value={Value}", claim.Type, claim.Value);
                }
                return null;
            }

            var fixedSalt = PasswordHasher.GetDeterministicSalt();
            var inputHash = PasswordHasher.HashDeterministic(usernameClaim.ToLowerInvariant(), fixedSalt);

            _logger.LogInformation("PlatformController: Identity found: {usernameClaim}. Looking up hashed user...", usernameClaim);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UsernameHashed == inputHash || u.EmailHashed == inputHash);

            if (user == null)
            {
                _logger.LogWarning("PlatformController: WARNING - No user found in database for identity {usernameClaim}", usernameClaim);
            }
            else
            {
                _logger.LogInformation("PlatformController: User found in database. AccountID: {AccountID}", user.AccountID);
            }

            return user;
        }

        private async Task<int> GetAccountCountForPlatform(string connectionString, string tableName)
        {
            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();
                
                // Safety check: verify table exists before counting
                string checkQuery = $"IF OBJECT_ID(N'[{tableName}]', N'U') IS NOT NULL SELECT COUNT(*) FROM [{tableName}] ELSE SELECT 0";
                
                using var command = new SqlCommand(checkQuery, connection);
                return Convert.ToInt32(await command.ExecuteScalarAsync());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not count records for table {tableName}", tableName);
                return 0;
            }
        }
    }

    public class PlatformApiModel
    {
        public string Name { get; set; } = string.Empty;
        public int AccountCount { get; set; }
    }

    public class EditPlatformRequest
    {
        public string OldName { get; set; } = string.Empty;
        public string NewName { get; set; } = string.Empty;
    }

    public class DeletePlatformRequest
    {
        public string Name { get; set; } = string.Empty;
        public string ConfirmName { get; set; } = string.Empty;
    }
}