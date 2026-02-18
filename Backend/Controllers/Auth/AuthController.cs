using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Security.Claims;
using System.Security.Cryptography;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using Backend.Data;
using Backend.Models;
using Backend.Models.Register;
using Backend.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ILogger<AuthController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly EncryptionService _encryptionService; 

        public AuthController(
            ILogger<AuthController> logger,
            ApplicationDbContext context,
            EncryptionService encryptionService)
        {
            _logger = logger;
            _context = context;
            _encryptionService = encryptionService;
        }

        #region Helper Endpoints


        [HttpGet("check-username")]
        public async Task<IActionResult> CheckUsername([FromQuery] string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return BadRequest(new { Message = "Username is required" });
            }

            var fixedSalt = PasswordHasher.GetDeterministicSalt();
            var usernameHash = PasswordHasher.HashDeterministic(username.ToLowerInvariant(), fixedSalt);
            
            bool exists = await _context.Users.AnyAsync(u => u.UsernameHashed == usernameHash);

            return Ok(new { IsAvailable = !exists });
        }

        [HttpGet("check-email")]
        public async Task<IActionResult> CheckEmail([FromQuery] string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return BadRequest(new { Message = "Email is required" });
            }

            var fixedSalt = PasswordHasher.GetDeterministicSalt();
            var emailHash = PasswordHasher.HashDeterministic(email.ToLowerInvariant(), fixedSalt);
            
            bool exists = await _context.Users.AnyAsync(u => u.EmailHashed == emailHash);

            return Ok(new { IsAvailable = !exists });
        }

        #endregion

        /*
         * LOGIC: REGISTRATION (ZK v2 - Argon2id)
         * 1. Hashing: Hash Password, Username, Email, VaultPassword using Argon2id.
         * 2. Key Derivation: Hashed VaultPassword becomes DerivedVaultKey.
         * 3. Generation: Create RandomVerifier (Guid).
         * 4. Encryption: Encrypt Username, Email, RandomVerifier using DerivedVaultKey (AES-GCM).
         * 5. Storage: Store Encrypted values and Hashed credentials (Lookup hashes + Password hash).
         * 6. Memory Cleanup: Nullify sensitive vars.
         */
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterModel model)
        {
            _logger.LogInformation("Processing registration (ZK v2).");
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                if (model.Password == model.VaultPassword)
                {
                    return BadRequest(new { Message = "Password and vault password must be different" });
                }

                var normalizedUsername = model.Username.ToLowerInvariant();
                var normalizedEmail = model.Email.ToLowerInvariant();
                var fixedSalt = PasswordHasher.GetDeterministicSalt();

                // Check for existing users via Deterministic Argon2id Hash
                var usernameHash = PasswordHasher.HashDeterministic(normalizedUsername, fixedSalt);
                if (await _context.Users.AnyAsync(u => u.UsernameHashed == usernameHash))
                {
                    return BadRequest(new { Message = "Username already exists" });
                }

                var emailHash = PasswordHasher.HashDeterministic(normalizedEmail, fixedSalt);
                if (await _context.Users.AnyAsync(u => u.EmailHashed == emailHash))
                {
                    return BadRequest(new { Message = "Email already exists" });
                }

                // --- Zero-Knowledge Encryption Flow ---
                
                // 1. Derive Key from Vault Password (Argon2id)
                // We use a RANDOM salt for deriving the key to ensure strength.
                // Wait, if we use a random salt, how do we derive the SAME key again during verification?
                // The salt must be STORED or DETERMINISTIC.
                // Standard practice: Store the salt used for key derivation in the DB alongside the encrypted data?
                // OR determine the salt from something constant?
                // User Prompt: "system first hashes the vault password... using argon2id> system uses the hashed vault password which would be the derived key"
                // "stores on the memory > encrypts..."
                // "User logs in... system verifies it." (This verifies Account Password).
                // "redirects to vault password verification > user enters vault password > system hashes... > decrypts..."
                // If we use a random salt for the Vault Password hash during registration, we MUST store that salt to reproduce the hash/key later.
                // BUT the prompt says "The hashed vault password would be use as the key".
                // We will generate a random salt, use it to derive the key, and... where do we store the salt?
                // We will store the salt as part of the `RandomVerifier` encrypted bundle? No, we need the key to decrypt it.
                // We should store the salt in a new column? No schema change for 'Salt' column allowed by prompt constraints (only RandomVerifier repurpose).
                // "The hashed vault password would be stored on the memory only".
                // Maybe the salt is part of the `PasswordHash` for the account? No.
                // Perhaps we use the `UsernameHashed` SALT? No.
                // We will use the SAME Deterministic Salt (Pepper) for the Vault Password derivation?
                // This reduces security slightly (same salt for all users' vault keys) but fits the "no schema change" constraint unless we store the salt in the `RandomVerifier` string (e.g. Salt:EncryptedData)?
                // Let's assume we can store the salt in the `RandomVerifier` column *implicitly* or use the Fixed Salt.
                // Using Fixed Salt for Vault Password derivation allows us to reproduce the key without extra storage.
                // User requirement: "instead of SHA512, we would use Argon2id".
                // I will use the `fixedSalt` (Backend Secret based) for deriving the Vault Key too, to ensure we can reproduce it.
                
                // WAIT. "system stores the data on the database".
                // If we use a random salt, we can't derive the key again.
                // Unless the PROMPT implies we store the salt?
                // "The hashed vault password should be stored on the memory only".
                // I'll use the Fixed Salt (Application Pepper) for Vault Password derivation. This guarantees I can re-derive it.
                
                byte[] vaultKeyBytes = PasswordHasher.DeriveKeyFromVaultPassword(model.VaultPassword, fixedSalt);

                // 2. Generate Random Verifier
                var randomVerifier = Guid.NewGuid().ToString();

                // 3. Encrypt Data using Derived Key (AES-GCM)
                string encryptedUsername = _encryptionService.Encrypt(model.Username, vaultKeyBytes);
                string encryptedEmail = _encryptionService.Encrypt(model.Email, vaultKeyBytes);
                string encryptedVerifier = _encryptionService.Encrypt(randomVerifier, vaultKeyBytes);

                // 4. Hash Account Password (Argon2id - Random Salt is fine here as we store the hash string which includes the salt)
                string passwordHash = PasswordHasher.HashPassword(model.Password);

                var user = new User
                {
                    Username = encryptedUsername,
                    Email = encryptedEmail,
                    UsernameHashed = usernameHash,
                    EmailHashed = emailHash,
                    PasswordHash = passwordHash,
                    RandomVerifier = encryptedVerifier, // Storing encrypted verifier in the repurposed column
                    Timezone = model.Timezone,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                // 5. Clear sensitive data
                Array.Clear(vaultKeyBytes, 0, vaultKeyBytes.Length);
                model.Password = string.Empty;
                model.VaultPassword = string.Empty;

                // Auto-login after registration
                var token = GenerateJwtToken(model.Username, "initial_" + Guid.NewGuid().ToString());

                return Ok(new
                {
                    Message = "Registration successful",
                    Token = token,
                    Username = model.Username, 
                    Email = model.Email
                });
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex, "Error during registration");
                return StatusCode(500, new { Message = $"An error occurred during registration: {ex.Message}" });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginModel model)
        {
            _logger.LogInformation("Login attempt received.");

            try
            {
                var normalizedInput = model.Username.ToLowerInvariant();
                var fixedSalt = PasswordHasher.GetDeterministicSalt();
                var inputHash = PasswordHasher.HashDeterministic(normalizedInput, fixedSalt);

                // Look up by Deterministic Hash
                var user = await _context.Users.FirstOrDefaultAsync(u => u.UsernameHashed == inputHash || u.EmailHashed == inputHash);

                // Verify Account Password
                if (user == null)
                {
                    _logger.LogWarning($"Login: User not found for '{model.Username}' (Hash: {inputHash})");
                    return Unauthorized(new { Message = "Invalid username or password" });
                }

                if (!PasswordHasher.VerifyPassword(user.PasswordHash, model.Password))
                {
                    _logger.LogWarning($"Login: Password mismatch for user '{model.Username}'");
                    return Unauthorized(new { Message = "Invalid username or password" });
                }

                _logger.LogInformation($"Login: SUCCESS for user '{model.Username}'");

                // Update last login time
                user.LastLoginAt = DateTime.UtcNow;

                // Create Session
                var session = new Session
                {
                    AccountID = user.AccountID,
                    LoginTime = DateTime.UtcNow,
                    IPAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = HttpContext.Request.Headers["User-Agent"].ToString()
                };
                _context.Sessions.Add(session);
                await _context.SaveChangesAsync();
                
                // Issue Token
                var token = GenerateJwtToken(model.Username, session.SessionId.ToString());

                return Ok(new
                {
                    Token = token,
                    Username = model.Username // Returning the input username as we can't decrypt the stored one yet
                });
            }
            catch (Exception ex)
            {
                var logPath = Path.Combine(Directory.GetCurrentDirectory(), "debug_logs.txt");
                System.IO.File.AppendAllText(logPath, $"[{DateTime.UtcNow}] Login Error: {ex.Message}\n");
                _logger.LogError(ex, "Error during login");
                return StatusCode(500, new { Message = "An error occurred during login" });
            }
        }
        
        // Helper Endpoint for RouteGuard Verification (Validation Only)
        // This endpoint verifies if the provided Vault Password can decrypt the stored Random Verify.
        [HttpPost("verify-vault-password")]
        [Authorize]
        public async Task<IActionResult> VerifyVaultPassword([FromBody] VerifyVaultPasswordModel model)
        {
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

                 Console.WriteLine($"[VerifyVaultPassword] Request for: {usernameClaim ?? "UNKNOWN"}");

                 if (string.IsNullOrEmpty(usernameClaim)) return Unauthorized(new { Message = "User identity not found in token." });

                 var fixedSalt = PasswordHasher.GetDeterministicSalt();
                 var inputHash = PasswordHasher.HashDeterministic(usernameClaim.ToLowerInvariant(), fixedSalt);
                 
                 // Fix: Check both UsernameHashed and EmailHashed
                 var user = await _context.Users.FirstOrDefaultAsync(u => u.UsernameHashed == inputHash || u.EmailHashed == inputHash);
                 if (user == null) 
                 {
                     Console.WriteLine($"[VerifyVaultPassword] REJECTED: User not found for '{usernameClaim}'");
                     return Unauthorized(new { Message = "User not found in database." });
                 }
                 
                 // Attempt Decrypt
                 Console.WriteLine($"[VerifyVaultPassword] Verifying user '{usernameClaim}'...");
                 Console.WriteLine($"[VerifyVaultPassword] DB RandomVerifier (First 10): {user.RandomVerifier.Substring(0, Math.Min(10, user.RandomVerifier.Length))}...");

                 byte[] vaultKeyBytes = PasswordHasher.DeriveKeyFromVaultPassword(model.VaultPassword, fixedSalt);
                 
                 Console.WriteLine("[VerifyVaultPassword] Attempting decryption...");
                 string decrypted = _encryptionService.Decrypt(user.RandomVerifier, vaultKeyBytes);
                 Console.WriteLine("[VerifyVaultPassword] SUCCESS: Decryption worked!");
                 
                 return Ok(new { IsAuthorized = true, Message = "Verification successful" });
             }
             catch (Exception ex)
             {
                 _logger.LogError(ex, "Error checking vault pass");
                 return StatusCode(500, new { Message = "Error checking vault password" });
             }
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var sessionIdClaim = User.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti);
                if (sessionIdClaim == null || !long.TryParse(sessionIdClaim.Value, out long sessionId))
                {
                    return BadRequest(new { Message = "Invalid session identifier in token." });
                }

                var session = await _context.Sessions.FirstOrDefaultAsync(s => s.SessionId == sessionId);

                if (session == null || session.LogoutTime.HasValue)
                {
                    return BadRequest(new { Message = "Session not found or already terminated." });
                }

                session.LogoutTime = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return Ok(new { Message = "Logout successful" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                return StatusCode(500, new { Message = "An error occurred during logout." });
            }
        }

        private string GenerateJwtToken(string username, string sessionId)
        {
            try
            {
                var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET");
                var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER");
                var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE");

                 if (string.IsNullOrEmpty(jwtSecret) || string.IsNullOrEmpty(jwtIssuer) || string.IsNullOrEmpty(jwtAudience))
                {
                    _logger.LogError("JWT environment variables are missing.");
                    throw new InvalidOperationException("JWT settings are not configured.");
                }

                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                var claims = new[]
                {
                    new Claim(JwtRegisteredClaimNames.Sub, username),
                    new Claim(JwtRegisteredClaimNames.UniqueName, username),
                    new Claim(JwtRegisteredClaimNames.Jti, sessionId) 
                };

                var token = new JwtSecurityToken(
                    issuer: jwtIssuer,
                    audience: jwtAudience,
                    claims: claims,
                    expires: DateTime.Now.AddDays(1),
                    signingCredentials: creds
                );

                return new JwtSecurityTokenHandler().WriteToken(token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating JWT token");
                throw;
            }
        }
    }

    public class LoginModel
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
    
    public class VerifyVaultPasswordModel
    {
        public string VaultPassword { get; set; } = string.Empty;
    }
}