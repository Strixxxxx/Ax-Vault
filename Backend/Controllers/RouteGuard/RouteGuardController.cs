using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Security.Cryptography;
using Backend.Data;
using Backend.Models.RouteGuard;
using Backend.Services;

namespace Backend.Controllers.RouteGuard
{
    [ApiController]
    [Route("api/[controller]")]
    public class RouteGuardController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<RouteGuardController> _logger;
        private readonly EncryptionService _encryptionService;

        public RouteGuardController(ApplicationDbContext context, ILogger<RouteGuardController> logger, EncryptionService encryptionService)
        {
            _context = context;
            _logger = logger;
            _encryptionService = encryptionService;
        }


        [HttpPost("validate")]
        [Authorize]
        public async Task<IActionResult> Validate([FromBody] RouteGuardRequest request)
        {
            var logPath = Path.Combine(Directory.GetCurrentDirectory(), "debug_logs.txt");
            try
            {
                // Robust username extraction
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
                    _logger.LogWarning("RouteGuardController: No identity claim found in token.");
                    _logger.LogWarning("RouteGuardController: Listing all available claims for debugging:");
                    foreach (var claim in User.Claims)
                    {
                        _logger.LogWarning("RouteGuardController: Claim: Type={Type}, Value={Value}", claim.Type, claim.Value);
                    }
                }
                
                // Fallback to explicitly passed identifier if claim is missing
                var identifier = usernameClaim ?? request.UserIdentifier;

                var logMsg = $"[{DateTime.UtcNow}] Validate: {identifier ?? "UNKNOWN"}, Claim: {usernameClaim ?? "NULL"}, BodyID: {request.UserIdentifier ?? "NULL"}\n";
                await System.IO.File.AppendAllTextAsync(logPath, logMsg);
                Console.WriteLine(logMsg);

                if (string.IsNullOrEmpty(identifier)) 
                {
                    await System.IO.File.AppendAllTextAsync(logPath, "[RouteGuard] REJECTED: No identifier found.\n");
                    return Unauthorized(new { Message = "User identity not found." });
                }

                var fixedSalt = PasswordHasher.GetDeterministicSalt();
                var inputHash = PasswordHasher.HashDeterministic(identifier.ToLowerInvariant(), fixedSalt);

                // Fix: Check both UsernameHashed and EmailHashed
                var user = await _context.Users.FirstOrDefaultAsync(u => u.UsernameHashed == inputHash || u.EmailHashed == inputHash);

                if (user == null)
                {
                    await System.IO.File.AppendAllTextAsync(logPath, $"[RouteGuard] REJECTED: User '{identifier}' not found in DB.\n");
                    return Unauthorized(new { Message = "User not found in database." });
                }

                if (string.IsNullOrEmpty(request.VaultPassword))
                {
                    _logger.LogWarning("RouteGuard: Vault Password is missing in request");
                    return BadRequest(new { Message = "Vault Password is required" });
                }

                Console.WriteLine($"[RouteGuard] Attempting verification for user '{usernameClaim}'");
                Console.WriteLine($"[RouteGuard] DB RandomVerifier (First 10): {user.RandomVerifier.Substring(0, Math.Min(10, user.RandomVerifier.Length))}...");

                // --- Verification Logic (Argon2id + Decryption) ---
                // 1. Derive Key from Vault Password
                Console.WriteLine("[RouteGuard] Deriving key from vault password...");
                byte[] vaultKeyBytes = PasswordHasher.DeriveKeyFromVaultPassword(request.VaultPassword, fixedSalt);
                
                try
                {
                    // 2. Attempt Decrypt RandomVerifier
                    string decrypted = _encryptionService.Decrypt(user.RandomVerifier, vaultKeyBytes);
                    
                    // Success!
                    await System.IO.File.AppendAllTextAsync(logPath, $"[RouteGuard] SUCCESS: Access granted to {identifier}\n");
                    return Ok(new { IsAuthorized = true, Message = "Access granted" });
                }
                catch (CryptographicException)
                {
                    await System.IO.File.AppendAllTextAsync(logPath, $"[RouteGuard] REJECTED: Decryption failed for {identifier}\n");
                    return Ok(new { IsAuthorized = false, Message = "Invalid vault password" }); 
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating route access");
                return StatusCode(500, new { Message = "An error occurred during validation" });
            }
        }
    }
}