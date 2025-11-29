using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using Backend.Models;
using Backend.Models.RouteGuard;
using Backend.Services;
using System.Threading.Tasks;

namespace Backend.Controllers.RouteGuard
{
    [ApiController]
    [Route("api/[controller]")]
    public class RouteGuardController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<RouteGuardController> _logger;

        public RouteGuardController(ApplicationDbContext context, ILogger<RouteGuardController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpPost("validate")]
        public async Task<IActionResult> ValidateAccess([FromBody] RouteGuardRequest request)
        {
            try
            {
                _logger.LogInformation($"Route guard validation request for user: {request.Username}, module: {request.TargetModule}");
                
                // Get all users and find the exact matching one using case-sensitive comparison
                var allUsers = await _context.Users.ToListAsync();
                
                // Find exact username match (case-sensitive)
                var user = allUsers.FirstOrDefault(u => u.Username.Equals(request.Username, StringComparison.Ordinal));
                
                if (user == null)
                {
                    _logger.LogWarning($"User not found: {request.Username}");
                    return Unauthorized(new RouteGuardResponse
                    {
                        IsAuthorized = false,
                        Message = "User not found"
                    });
                }
                
                _logger.LogInformation($"User found: {user.Username}, validating unique key");
                
                // Log hashed key format for debugging (safely)
                var keyParts = user.UniqueKey.Split('.');
                var hashFormat = keyParts.Length == 3 
                    ? $"Hash format valid: {keyParts[0]} iterations"
                    : $"Invalid hash format: expected 3 parts, got {keyParts.Length}";
                
                _logger.LogInformation(hashFormat);
                
                // Validate the unique key
                bool isValid = PasswordHasher.VerifyPassword(user.UniqueKey, request.UniqueKey);
                _logger.LogInformation($"Key validation result: {isValid}");
                
                if (!isValid)
                {
                    return Unauthorized(new RouteGuardResponse
                    {
                        IsAuthorized = false,
                        Message = "Invalid unique key"
                    });
                }
                
                _logger.LogInformation($"Route guard validation successful for user: {request.Username}, module: {request.TargetModule}");
                
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