using System.Net;
using System.Net.Mail;

namespace Backend.Services
{
    public class EmailService
    {
        private readonly ILogger<EmailService> _logger;

        public EmailService(ILogger<EmailService> logger)
        {
            _logger = logger;
        }

        public async Task<bool> SendOtpEmailAsync(string toEmail, string otp)
        {
            var smtpHost = Environment.GetEnvironmentVariable("SMTP_HOST");
            var smtpPort = Environment.GetEnvironmentVariable("SMTP_PORT");
            var smtpUser = Environment.GetEnvironmentVariable("SMTP_USER");
            var smtpPass = Environment.GetEnvironmentVariable("SMTP_PASS");
            var fromEmail = Environment.GetEnvironmentVariable("SMTP_FROM") ?? smtpUser;

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
