using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using Backend.Data;
using Backend.Models;
using Backend.Models.Login;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;

namespace Backend.Controllers.Login
{
    [ApiController]
    [Route("api/[controller]")]
    public class LoginController : ControllerBase
    {
        private readonly ILogger<LoginController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly EncryptionService _encryptionService;

        public LoginController(
            ILogger<LoginController> logger,
            ApplicationDbContext context,
            EncryptionService encryptionService)
        {
            _logger = logger;
            _context = context;
            _encryptionService = encryptionService;
        }

        [HttpPost]
        public async Task<IActionResult> Login([FromBody] LoginModel model)
        {
            _logger.LogInformation($"Login attempt for user: {model.Username}");

            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new { Message = "Invalid login data" });
                }

                var normalizedInput = model.Username.ToLowerInvariant();
                var inputHash = PasswordHasher.HashPassword(normalizedInput);

                var user = await _context.Users.FirstOrDefaultAsync(u => u.UsernameHashed == inputHash || u.EmailHashed == inputHash);

                if (user == null || !PasswordHasher.VerifyPassword(user.PasswordHash, model.Password))
                {
                    return Unauthorized(new { Message = "Invalid username/email or password" });
                }

                // Decrypt sensitive information for response and token
                var decryptedUsername = _encryptionService.Decrypt(user.Username);
                var decryptedEmail = _encryptionService.Decrypt(user.Email);

                // Update last login time
                user.LastLoginAt = DateTime.UtcNow;

                // Create a new session record
                var session = new Session
                {
                    AccountID = user.AccountID,
                    LoginTime = DateTime.UtcNow,
                    IPAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = HttpContext.Request.Headers["User-Agent"].ToString()
                };
                _context.Sessions.Add(session);

                await _context.SaveChangesAsync();
                
                var token = GenerateJwtToken(decryptedUsername, session.SessionId.ToString());

                return Ok(new LoginResponseModel
                {
                    Token = token,
                    Username = decryptedUsername,
                    Email = decryptedEmail,
                    DatabaseName = user.DatabaseName,
                    Timezone = user.Timezone
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login");
                return StatusCode(500, new { Message = "An error occurred during login" });
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
                    return BadRequest("Invalid session identifier in token.");
                }

                var session = await _context.Sessions.FirstOrDefaultAsync(s => s.SessionId == sessionId);

                if (session == null || session.LogoutTime.HasValue)
                {
                    return BadRequest("Session not found or already terminated.");
                }

                session.LogoutTime = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return Ok(new { Message = "Logout successful" });
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                return StatusCode(500, new { Message = "An error occurred during logout." });
            }
        }

        private string GenerateJwtToken(string username, string sessionId)
        {
            var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET");
            var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER");
            var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE");

            if (string.IsNullOrEmpty(jwtSecret) || string.IsNullOrEmpty(jwtIssuer) || string.IsNullOrEmpty(jwtAudience))
            {
                _logger.LogError("JWT environment variables not set");
                throw new InvalidOperationException("JWT settings are not configured");
            }
            
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, username),
                new Claim(JwtRegisteredClaimNames.Jti, sessionId) // Use JTI for session ID
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
    }
}