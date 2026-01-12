using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using Backend.Models;
using Backend.Models.RouteGuard;
using Backend.Services;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace Backend.Controllers.RouteGuard
{
    [ApiController]
    [Route("api/[controller]")]
    public class RouteGuardController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<RouteGuardController> _logger;
        private readonly EncryptionService _encryptionService;

        public RouteGuardController(
            ApplicationDbContext context, 
            ILogger<RouteGuardController> logger, 
            EncryptionService encryptionService)
        {
            _context = context;
            _logger = logger;
            _encryptionService = encryptionService;
        }

        [HttpPost("validate")]
        [Authorize]
        public async Task<IActionResult> ValidateAccess([FromBody] RouteGuardRequest request)
        {
            try
            {
                _logger.LogInformation("=== ROUTE GUARD VALIDATION REQUEST RECEIVED ===");
                _logger.LogInformation($"Request - TargetModule: {request.TargetModule}, UniqueKey Length: {request.UniqueKey?.Length ?? 0}");
                
                if (string.IsNullOrEmpty(request.UniqueKey))
                {
                    _logger.LogWarning("Request is missing the unique key.");
                    return BadRequest(new RouteGuardResponse { IsAuthorized = false, Message = "Unique key is required." });
                }

                // Get username from the JWT token's subject claim - Robust Lookup
                var username = User.Identity?.Name;
                
                if (string.IsNullOrEmpty(username)) 
                {
                    // Fallback: Check for other common claim types if Identity.Name is null
                    username = User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value 
                              ?? User.Claims.FirstOrDefault(c => c.Type == "unique_name")?.Value
                              ?? User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                              ?? User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Name)?.Value;
                }
                
                _logger.LogInformation($"JWT Token username claim: {username}");
                
                if (string.IsNullOrEmpty(username))
                {
                    _logger.LogWarning("Username claim not found in token");
                    _logger.LogWarning("--- AVAILABLE CLAIMS ---");
                    foreach (var claim in User.Claims)
                    {
                        _logger.LogWarning($"Type: {claim.Type}, Value: {claim.Value}");
                    }
                    _logger.LogWarning("------------------------");
                    return Unauthorized(new { Message = "Username claim not found in token." });
                }

                _logger.LogInformation($"Route guard validation request for user: {username}, module: {request.TargetModule}");

                // Generate the deterministic hash for the username
                var usernameHash = EncryptionService.GenerateDeterministicHash(username.ToLowerInvariant());
                _logger.LogInformation($"Generated username hash for lookup: {usernameHash.Substring(0, 10)}...");

                // Find the user by their hashed username
                var user = await _context.Users.FirstOrDefaultAsync(u => u.UsernameHashed == usernameHash);

                if (user == null)
                {
                    _logger.LogWarning($"User from token not found in database: {username}");
                    return Unauthorized(new RouteGuardResponse
                    {
                        IsAuthorized = false,
                        Message = "User not found"
                    });
                }
                
                // Decrypt username for logging now that we have the correct user object
                var decryptedUsername = _encryptionService.Decrypt(user.Username);
                _logger.LogInformation($"User found in database: {decryptedUsername}");
                
                // Log the stored unique key from the database for debugging
                _logger.LogInformation($"Stored Unique Key from DB (first 15 chars): {user.UniqueKey.Substring(0, Math.Min(15, user.UniqueKey.Length))}...");

                // Log hashed key format for debugging (safely)
                var keyParts = user.UniqueKey.Split('.');
                if (keyParts.Length == 3)
                {
                    _logger.LogInformation($"DB Key Iterations: {keyParts[0]}");
                    _logger.LogInformation($"DB Key Salt (Base64): {keyParts[1]}");
                    _logger.LogInformation($"DB Key Hash (Base64): {keyParts[2]}");
                }
                else
                {
                    _logger.LogError($"Invalid stored key format for user {decryptedUsername}. Expected 3 parts, found {keyParts.Length}. Stored key starts with: {user.UniqueKey.Substring(0, Math.Min(15, user.UniqueKey.Length))}");
                    return StatusCode(500, new RouteGuardResponse { IsAuthorized = false, Message = "Server configuration error: Invalid key format." });
                }
                
                // Validate the unique key
                _logger.LogInformation("--- HASH VERIFICATION ---");
                _logger.LogInformation($"Calling PasswordHasher.VerifyPassword with StoredHash='{user.UniqueKey}' and ProvidedKey='{request.UniqueKey}'");
                bool isValid = PasswordHasher.VerifyPassword(user.UniqueKey, request.UniqueKey);
                _logger.LogInformation($"=== KEY VALIDATION RESULT: {isValid} ===");
                
                if (!isValid)
                {
                    return Unauthorized(new RouteGuardResponse
                    {
                        IsAuthorized = false,
                        Message = "Invalid unique key"
                    });
                }
                
                _logger.LogInformation($"Route guard validation successful for user: {decryptedUsername}, module: {request.TargetModule}");
                
                // If we reach here, the user is authorized to access the module
                return Ok(new RouteGuardResponse
                {
                    IsAuthorized = true,
                    Message = $"Access granted to {request.TargetModule}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during route guard validation");
                return StatusCode(500, new RouteGuardResponse
                {
                    IsAuthorized = false,
                    Message = "An error occurred during validation"
                });
            }
        }
    }
} 