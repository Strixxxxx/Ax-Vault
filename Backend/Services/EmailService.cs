using System.Net;
using System.Net.Mail;

namespace Backend.Services
{
    public class EmailService
    {
        private readonly ILogger<EmailService> _logger;
        private readonly IConfiguration _configuration;

        public EmailService(ILogger<EmailService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        private string? GetSetting(string key) => _configuration[key] ?? Environment.GetEnvironmentVariable(key);

        public async Task<bool> SendOtpEmailAsync(string toEmail, string otp)
        {
            var smtpHost = GetSetting("SMTP_HOST");
            var smtpPort = GetSetting("SMTP_PORT");
            var smtpUser = GetSetting("SMTP_USER");
            var smtpPass = GetSetting("SMTP_PASS");
            var fromEmail = GetSetting("SMTP_FROM") ?? smtpUser;

            // If SMTP is not configured, fallback to console logging for dev/testing
            if (string.IsNullOrEmpty(smtpHost) || string.IsNullOrEmpty(smtpUser) || string.IsNullOrEmpty(smtpPass))
            {
                _logger.LogWarning("==========================================================");
                _logger.LogWarning("[DEV MODE] SMTP not configured. OTP for {Email}: {Otp}", toEmail, otp);
                _logger.LogWarning("==========================================================");
                Console.WriteLine($"\n[DEV OTP] Email: {toEmail} | OTP: {otp}\n");
                return true;
            }

            try
            {
                int port = int.TryParse(smtpPort, out int p) ? p : 587;

                using var client = new SmtpClient(smtpHost, port)
                {
                    Credentials = new NetworkCredential(smtpUser, smtpPass),
                    EnableSsl = true
                };

                var mail = new MailMessage
                {
                    From = new MailAddress(fromEmail!, "Ax Vault"),
                    Subject = "Your Ax Vault Password Recovery OTP",
                    Body = $@"
Hello,

You have requested to reset your Ax Vault account password.

Your One-Time Password (OTP) is:

    {otp}

This OTP is valid for 10 minutes. Do not share it with anyone.

If you did not request this, please ignore this email.

— Ax Vault Security Team
",
                    IsBodyHtml = false
                };
                mail.To.Add(toEmail);

                await client.SendMailAsync(mail);
                _logger.LogInformation("OTP email sent to {Email}", toEmail);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send OTP email to {Email}", toEmail);
                return false;
            }
        }
    }
}
