using Microsoft.AspNetCore.Mvc;
using Backend.Data;
using Backend.Models;
using Backend.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/forgot-password")]
    public class ForgotPasswordController : ControllerBase
    {
        private readonly ILogger<ForgotPasswordController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly EncryptionService _encryptionService;
        private readonly EmailService _emailService;
        private readonly PasswordHasher _passwordHasher;
        private readonly IConfiguration _configuration;

        public ForgotPasswordController(
            ILogger<ForgotPasswordController> logger,
            ApplicationDbContext context,
            EncryptionService encryptionService,
            EmailService emailService,
            PasswordHasher passwordHasher,
            IConfiguration configuration)
        {
            _logger = logger;
            _context = context;
            _encryptionService = encryptionService;
            _emailService = emailService;
            _passwordHasher = passwordHasher;
            _configuration = configuration;
        }

        // ─── Step 1: Verify Username/Email ─────────────────────────────────────

        [HttpPost("verify-username")]
        public async Task<IActionResult> VerifyUsername([FromBody] ForgotPasswordUsernameModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Username))
                return BadRequest(new { Message = "Username or Email is required." });

            var fixedSalt = _passwordHasher.GetDeterministicSalt();
            var inputHash = _passwordHasher.HashDeterministic(model.Username.ToLowerInvariant(), fixedSalt);

            // Check both username and email hashes, same as login
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UsernameHashed == inputHash || u.EmailHashed == inputHash);
            if (user == null)
            {
                _logger.LogWarning("[ForgotPassword] Identity '{User}' not found.", model.Username);
                return NotFound(new { Message = "Account not found." });
            }

            _logger.LogInformation("[ForgotPassword] Identity '{User}' verified.", model.Username);
            return Ok(new { Message = "Account found.", AccountId = user.AccountID });
        }

        // ─── Step 2a: Send OTP ─────────────────────────────────────────────────

        [HttpPost("send-otp")]
        public async Task<IActionResult> SendOtp([FromBody] ForgotPasswordSendOtpModel model)
        {
            if (model.AccountId <= 0)
                return BadRequest(new { Message = "Invalid account identifier." });

            var user = await _context.Users.FindAsync(model.AccountId);
            if (user == null)
                return NotFound(new { Message = "Account not found." });

            // Generate a random 6-digit OTP
            var otp = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
            user.OtpCode = otp;
            user.OtpExpiry = DateTime.UtcNow.AddMinutes(10);
            await _context.SaveChangesAsync();

            // Decrypt user's email to send OTP
            string plaintextEmail;
            try
            {
                var fixedSalt = _passwordHasher.GetDeterministicSalt();
                byte[] vaultKeyBytes = _passwordHasher.DeriveKeyFromVaultPassword(
                    _configuration["BACKEND_SECRET_KEY"] ?? Environment.GetEnvironmentVariable("BACKEND_SECRET_KEY") ?? string.Empty,
                    fixedSalt);
                plaintextEmail = _encryptionService.Decrypt(user.Email, vaultKeyBytes);
                Array.Clear(vaultKeyBytes, 0, vaultKeyBytes.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ForgotPassword] Failed to decrypt email for account {Id}.", model.AccountId);
                return StatusCode(500, new { Message = "Failed to retrieve email for this account. Please use vault password recovery instead." });
            }

            var sent = await _emailService.SendOtpEmailAsync(plaintextEmail, otp);
            if (!sent)
                return StatusCode(500, new { Message = "Failed to send OTP email. Please try again." });

            // Return masked email for display on frontend
            var maskedEmail = MaskEmail(plaintextEmail);
            _logger.LogInformation("[ForgotPassword] OTP sent to masked email: {Email}", maskedEmail);
            return Ok(new { Message = "OTP sent successfully.", MaskedEmail = maskedEmail });
        }

        // ─── Step 2b: Verify OTP ───────────────────────────────────────────────

        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] ForgotPasswordVerifyOtpModel model)
        {
            if (model.AccountId <= 0 || string.IsNullOrWhiteSpace(model.Otp))
                return BadRequest(new { Message = "Invalid request." });

            var user = await _context.Users.FindAsync(model.AccountId);
            if (user == null)
                return NotFound(new { Message = "Account not found." });

            if (string.IsNullOrEmpty(user.OtpCode) || user.OtpExpiry == null)
                return BadRequest(new { Message = "No OTP was requested. Please request a new OTP." });

            if (DateTime.UtcNow > user.OtpExpiry)
            {
                user.OtpCode = null;
                user.OtpExpiry = null;
                await _context.SaveChangesAsync();
                return BadRequest(new { Message = "OTP has expired. Please request a new one." });
            }

            if (user.OtpCode != model.Otp.Trim())
                return BadRequest(new { Message = "Incorrect OTP. Please try again." });

            // Invalidate OTP after successful use
            user.OtpCode = null;
            user.OtpExpiry = null;
            await _context.SaveChangesAsync();

            _logger.LogInformation("[ForgotPassword] OTP verified for account {Id}.", model.AccountId);
            return Ok(new { Message = "OTP verified successfully.", Verified = true });
        }

        // ─── Step 3: Verify Vault Password ────────────────────────────────────

        [HttpPost("verify-vault")]
        public async Task<IActionResult> VerifyVault([FromBody] ForgotPasswordVerifyVaultModel model)
        {
            if (model.AccountId <= 0 || string.IsNullOrWhiteSpace(model.VaultPassword))
                return BadRequest(new { Message = "Invalid request." });

            var user = await _context.Users.FindAsync(model.AccountId);
            if (user == null)
                return NotFound(new { Message = "Account not found." });

            try
            {
                var fixedSalt = _passwordHasher.GetDeterministicSalt();
                byte[] vaultKeyBytes = _passwordHasher.DeriveKeyFromVaultPassword(model.VaultPassword, fixedSalt);
                // Attempt decryption of RandomVerifier - if it succeeds, vault password is correct
                _encryptionService.Decrypt(user.RandomVerifier, vaultKeyBytes);
                Array.Clear(vaultKeyBytes, 0, vaultKeyBytes.Length);

                _logger.LogInformation("[ForgotPassword] Vault password verified for account {Id}.", model.AccountId);
                return Ok(new { Message = "Vault password verified.", Verified = true });
            }
            catch
            {
                return Unauthorized(new { Message = "Incorrect vault password." });
            }
        }

        // ─── Step 4: Reset Password ────────────────────────────────────────────

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ForgotPasswordResetModel model)
        {
            if (model.AccountId <= 0 || string.IsNullOrWhiteSpace(model.NewPassword))
                return BadRequest(new { Message = "Invalid request." });

            // Server-side password validation
            var pwd = model.NewPassword;
            if (pwd.Length < 9 || pwd.Length > 16)
                return BadRequest(new { Message = "Password must be between 9 and 16 characters." });
            if (!pwd.Any(char.IsUpper))
                return BadRequest(new { Message = "Password must contain at least one uppercase letter." });
            if (!pwd.Any(char.IsLower))
                return BadRequest(new { Message = "Password must contain at least one lowercase letter." });
            if (!pwd.Any(char.IsDigit))
                return BadRequest(new { Message = "Password must contain at least one number." });
            if (!pwd.Any(c => !char.IsLetterOrDigit(c)))
                return BadRequest(new { Message = "Password must contain at least one special character." });

            var user = await _context.Users.FindAsync(model.AccountId);
            if (user == null)
                return NotFound(new { Message = "Account not found." });

            user.PasswordHash = _passwordHasher.HashPassword(model.NewPassword);
            await _context.SaveChangesAsync();

            _logger.LogInformation("[ForgotPassword] Password reset for account {Id}.", model.AccountId);
            return Ok(new { Message = "Password reset successfully." });
        }

        // ─── Helpers ───────────────────────────────────────────────────────────

        private static string MaskEmail(string email)
        {
            var parts = email.Split('@');
            if (parts.Length != 2) return "****@****.***";
            var name = parts[0];
            var domain = parts[1];
            var maskedName = name.Length <= 2
                ? new string('*', name.Length)
                : name[0] + new string('*', name.Length - 2) + name[^1];
            return $"{maskedName}@{domain}";
        }
    }

    // ─── Request Models ─────────────────────────────────────────────────────────

    public class ForgotPasswordUsernameModel
    {
        public string Username { get; set; } = string.Empty;
    }

    public class ForgotPasswordSendOtpModel
    {
        public long AccountId { get; set; }
    }

    public class ForgotPasswordVerifyOtpModel
    {
        public long AccountId { get; set; }
        public string Otp { get; set; } = string.Empty;
    }

    public class ForgotPasswordVerifyVaultModel
    {
        public long AccountId { get; set; }
        public string VaultPassword { get; set; } = string.Empty;
    }

    public class ForgotPasswordResetModel
    {
        public long AccountId { get; set; }
        public string NewPassword { get; set; } = string.Empty;
    }
}
