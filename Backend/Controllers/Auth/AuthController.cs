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
        private readonly UserDatabaseService _userDatabaseService;
        private readonly EncryptionService _encryptionService;

        public AuthController(
            ILogger<AuthController> logger,
            ApplicationDbContext context,
            UserDatabaseService userDatabaseService,
            EncryptionService encryptionService)
        {
            _logger = logger;
            _context = context;
            _userDatabaseService = userDatabaseService;
            _encryptionService = encryptionService;
        }

        [HttpGet("check-username")]
        public async Task<IActionResult> CheckUsername([FromQuery] string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return BadRequest(new { Message = "Username is required" });
            }

            var usernameHash = PasswordHasher.HashPassword(username.ToLowerInvariant());
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

            var emailHash = PasswordHasher.HashPassword(email.ToLowerInvariant());
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
            Console.WriteLine($"Username: {model.Username}");
            Console.WriteLine($"Email: {model.Email}");
            Console.WriteLine($"Password Length: {model.Password.Length}");
            Console.WriteLine($"Unique Key Length: {model.UniqueKey.Length}");
            Console.WriteLine($"Timezone: {model.Timezone}");
            Console.WriteLine("---------------------------------");
            try
            {
                _logger.LogInformation($"Registration attempt for user: {model.Username}");

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

                var usernameHash = PasswordHasher.HashPassword(normalizedUsername);
                if (await _context.Users.AnyAsync(u => u.UsernameHashed == usernameHash))
                {
                    return BadRequest(new { Message = "Username already exists" });
                }

                var emailHash = PasswordHasher.HashPassword(normalizedEmail);
                if (await _context.Users.AnyAsync(u => u.EmailHashed == emailHash))
                {
                    return BadRequest(new { Message = "Email already exists" });
                }

                if (model.Password == model.UniqueKey)
                {
                    return BadRequest(new { Message = "Password and unique key must be different" });
                }

                string uniqueSuffix = DateTime.Now.ToString("yyyyMMddHHmmss");
                string safeUsername = model.Username.Replace(" ", "_");
                string databaseName = $"{safeUsername}_{uniqueSuffix}_db";

                await _userDatabaseService.CreateUserDatabaseAsync(model.Username, databaseName);
                _logger.LogInformation($"Created database '{databaseName}' for user '{model.Username}'");
                
                var user = new User
                {
                    Username = _encryptionService.Encrypt(model.Username),
                    Email = _encryptionService.Encrypt(model.Email),
                    UsernameHashed = usernameHash,
                    EmailHashed = emailHash,
                    PasswordHash = PasswordHasher.HashPassword(model.Password),
                    UniqueKey = PasswordHasher.HashPassword(model.UniqueKey),
                    DatabaseName = databaseName,
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
                    Username = model.Username,
                    Email = model.Email,
                    DatabaseName = user.DatabaseName
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
            _logger.LogInformation($"Login attempt for user: {model.Username}");

            try
            {
                var normalizedInput = model.Username.ToLowerInvariant();
                var inputHash = PasswordHasher.HashPassword(normalizedInput);

                var user = await _context.Users.FirstOrDefaultAsync(u => u.UsernameHashed == inputHash || u.EmailHashed == inputHash);

                if (user == null || !PasswordHasher.VerifyPassword(user.PasswordHash, model.Password))
                {
                    return Unauthorized(new { Message = "Invalid username or password" });
                }

                var decryptedUsername = _encryptionService.Decrypt(user.Username);
                var token = GenerateJwtToken(decryptedUsername);

                user.LastLoginAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                
                return Ok(new
                {
                    Token = token,
                    Username = decryptedUsername,
                    DatabaseName = user.DatabaseName
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

        [HttpPost("fix-database-permissions")]
        public async Task<IActionResult> FixDatabasePermissions([FromBody] FixDatabaseModel model)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(model.Username))
                {
                    return BadRequest(new { Message = "Username is required" });
                }

                var usernameHash = PasswordHasher.HashPassword(model.Username.ToLowerInvariant());
                var user = await _context.Users.FirstOrDefaultAsync(u => u.UsernameHashed == usernameHash);

                if (user == null)
                {
                    return NotFound(new { Message = "User not found" });
                }

                _logger.LogInformation($"Fixing database permissions for user {model.Username} (database: {user.DatabaseName})");

                await _userDatabaseService.EnsureDatabasePermissionsAsync(user.DatabaseName);

                return Ok(new { Message = $"Database permissions for {user.DatabaseName} fixed successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fixing database permissions");
                return StatusCode(500, new { Message = $"An error occurred while fixing database permissions: {ex.Message}" });
            }
        }
    }

    public class LoginModel
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class FixDatabaseModel
    {
        public string Username { get; set; } = string.Empty;
    }
}
 