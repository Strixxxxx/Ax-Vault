using System.Net.Http.Json;
using Frontend.Services;

namespace Frontend.Components.ForgotPassword.Steps
{
    public partial class OtpStep : ContentView
    {
        public event EventHandler? Proceed;
        public event EventHandler? BackRequested;

        private long _accountId;

        public OtpStep() { InitializeComponent(); }

        public void SetAccountInfo(long accountId, string maskedEmail)
        {
            _accountId = accountId;
            InfoLabel.Text = $"Enter the 6-digit code sent to {maskedEmail}.";
        }

        /// <summary>
        /// Sends an OTP request and returns the masked email on success, or null on failure.
        /// </summary>
        public async Task<string?> RequestOtp(long accountId)
        {
            _accountId = accountId;
            try
            {
                var response = await ApiClient.Instance.PostAsJsonAsync(
                    "api/forgot-password/send-otp",
                    new { AccountId = accountId });

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<SendOtpResponse>();
                    return result?.MaskedEmail;
                }
                else
                {
                    var err = await response.Content.ReadFromJsonAsync<ErrorResponse>();
                    StatusLabel.Text = err?.Message ?? "Failed to send OTP.";
                    StatusLabel.IsVisible = true;
                    return null;
                }
            }
            catch
            {
                StatusLabel.Text = "Could not connect to the server.";
                StatusLabel.IsVisible = true;
                return null;
            }
        }

        private async void OnVerifyClicked(object sender, EventArgs e)
        {
            var otp = OtpEntry.Text?.Trim();
            if (string.IsNullOrEmpty(otp) || otp.Length != 6)
            {
                StatusLabel.Text = "Please enter the 6-digit OTP.";
                StatusLabel.IsVisible = true;
                return;
            }

            StatusLabel.IsVisible = false;
            try
            {
                var response = await ApiClient.Instance.PostAsJsonAsync(
                    "api/forgot-password/verify-otp",
                    new { AccountId = _accountId, Otp = otp });

                if (response.IsSuccessStatusCode)
                {
                    Proceed?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    var err = await response.Content.ReadFromJsonAsync<ErrorResponse>();
                    StatusLabel.Text = err?.Message ?? "Invalid OTP.";
                    StatusLabel.IsVisible = true;
                }
            }
            catch
            {
                StatusLabel.Text = "Could not connect to the server.";
                StatusLabel.IsVisible = true;
            }
        }

        private async void OnResendTapped(object sender, EventArgs e)
        {
            StatusLabel.IsVisible = false;
            OtpEntry.Text = string.Empty;
            var maskedEmail = await RequestOtp(_accountId);
            if (maskedEmail != null)
            {
                InfoLabel.Text = $"A new code was sent to {maskedEmail}.";
            }
        }

        private void OnBackTapped(object sender, EventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);

        public void Reset()
        {
            OtpEntry.Text = string.Empty;
            StatusLabel.IsVisible = false;
            InfoLabel.Text = "Enter the 6-digit code sent to your email.";
        }

        private class SendOtpResponse { public string MaskedEmail { get; set; } = string.Empty; }
        private class ErrorResponse { public string Message { get; set; } = string.Empty; }
    }
}
