using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using Backend.Data;
using Backend.Models;
using Backend.Models.Register;
using Backend.Services;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ILogger<AuthController> _logger;
        private readonly ApplicationDbContext _context;
        // _userDatabaseService is no longer needed for DB creation
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

        [HttpGet("check-username")]
        public async Task<IActionResult> CheckUsername([FromQuery] string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return BadRequest(new { Message = "Username is required" });
            }

            var usernameHash = EncryptionService.GenerateDeterministicHash(username.ToLowerInvariant());
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

            var emailHash = EncryptionService.GenerateDeterministicHash(email.ToLowerInvariant());
            bool exists = await _context.Users.AnyAsync(u => u.EmailHashed == emailHash);

            return Ok(new { IsAvailable = !exists });
        }

        [HttpGet("hash-secret")]
        public IActionResult HashSecret([FromQuery] string secret)
        {
            if (string.IsNullOrWhiteSpace(secret))
            {
                return BadRequest(new { Message = "Secret to hash cannot be empty." });
            }
            var hashedSecret = PasswordHasher.HashPassword(secret);
            return Ok(new { secret = secret, hash = hashedSecret });
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterModel model)
        {
            Console.WriteLine("--- New Registration Attempt ---");
            Console.WriteLine($"Username (Hash): {EncryptionService.GenerateDeterministicHash(model.Username)}");
            Console.WriteLine("---------------------------------");
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                if (string.IsNullOrWhiteSpace(model.Timezone))
                {
                    return BadRequest(new { Message = "Timezone is required" });
                }

                var normalizedUsername = model.Username.ToLowerInvariant();
                var normalizedEmail = model.Email.ToLowerInvariant();

                var usernameHash = EncryptionService.GenerateDeterministicHash(normalizedUsername);
                if (await _context.Users.AnyAsync(u => u.UsernameHashed == usernameHash))
                {
                    return BadRequest(new { Message = "Username already exists" });
                }

                var emailHash = EncryptionService.GenerateDeterministicHash(normalizedEmail);
                if (await _context.Users.AnyAsync(u => u.EmailHashed == emailHash))
                {
                    return BadRequest(new { Message = "Email already exists" });
                }

                if (model.Password == model.VaultKey)
                {
                    return BadRequest(new { Message = "Password and vault key must be different" });
                }

                // Zero-Knowledge Encryption
                // 1. Derive Key from Vault Key
                string derivedKey = _encryptionService.DeriveKeyFromVaultKey(model.VaultKey);

                // 2. Encrypt Data using Derived Key
                string encryptedUsername = _encryptionService.Encrypt(model.Username, derivedKey);
                string encryptedEmail = _encryptionService.Encrypt(model.Email, derivedKey);

                // 3. Hash Vault Key for Storage (Verification only)
                string vaultKeyHash = PasswordHasher.HashPassword(model.VaultKey);

                // 4. Hash Password
                string passwordHash = PasswordHasher.HashPassword(model.Password);

                // Clear derived key from memory variables explicitly if possible (GC handles it eventually, but good practice to scope tightly)
                // In C#, strings are immutable, so we rely on scope exit.

                var user = new User
                {
                    Username = encryptedUsername,
                    Email = encryptedEmail,
                    UsernameHashed = usernameHash,
                    EmailHashed = emailHash,
                    PasswordHash = passwordHash,
                    VaultKey = vaultKeyHash, // Stores the Hashed Vault Key
                    DatabaseName = string.Empty, // No longer used
                    Timezone = model.Timezone,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                var token = GenerateJwtToken(model.Username);

                return Ok(new
                {
                    Message = "Registration successful",
                    Token = token,
                    // We cannot return decrypted data without the key, and we shouldn't persist the key.
                    // Returning the input values is fine as we are in the same session.
                    Username = model.Username, 
                    Email = model.Email,
                    DatabaseName = ""
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registration");
                return StatusCode(500, new { Message = $"An error occurred during registration: {ex.Message}" });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginModel model)
        {
            _logger.LogInformation($"Login attempt for user.");

            try
            {
                var normalizedInput = model.Username.ToLowerInvariant();
                var inputHash = EncryptionService.GenerateDeterministicHash(normalizedInput);

                var user = await _context.Users.FirstOrDefaultAsync(u => u.UsernameHashed == inputHash || u.EmailHashed == inputHash);

                if (user == null || !PasswordHasher.VerifyPassword(user.PasswordHash, model.Password))
                {
                    return Unauthorized(new { Message = "Invalid username or password" });
                }

                // In Zero-Knowledge, we CANNOT decrypt the username without the Vault Key.
                // The LoginModel currently does not include the Vault Key.
                // We will return the available info. 
                // The user's prompt implied "backend would remove the plaintext vault key... and goes back to login page".
                // We assume regular login verifies Password. Accessing Vault items will likely require Vault Key later in a separate step or session.
                
                // For now, return the Token. Use the Hashed Username or a placeholder for the Identity Name since we can't decrypt the real one.
                // Or use the Input Username since we verified the hash matches.
                
                string safeUsernameForToken = model.Username; 

                var token = GenerateJwtToken(safeUsernameForToken);

                user.LastLoginAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                
                return Ok(new
                {
                    Token = token,
                    Username = model.Username, // Return the input username since we confirmed it matches
                    DatabaseName = ""
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login");
                return StatusCode(500, new { Message = "An error occurred during login" });
            }
        }

        private string GenerateJwtToken(string username)
        {
            try
            {
                var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET");
                var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER");
                var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE");

                if (string.IsNullOrEmpty(jwtSecret) || string.IsNullOrEmpty(jwtIssuer) || string.IsNullOrEmpty(jwtAudience))
                {
                    _logger.LogError("One or more JWT environment variables are not set. Token generation failed.");
                    throw new InvalidOperationException("JWT settings are not configured. Check your .env file.");
                }

                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                var claims = new[]
                {
                    new Claim(JwtRegisteredClaimNames.Sub, username),
                    new Claim(JwtRegisteredClaimNames.UniqueName, username),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
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
}
 