using System.Net.Http.Json;
using Frontend.Services;

namespace Frontend.Components.ForgotPassword.Steps
{
    public partial class VaultStep : ContentView
    {
        public event EventHandler? Proceed;
        public event EventHandler? BackRequested;

        private long _accountId;
        private bool _isPasswordVisible = false;

        public VaultStep() { InitializeComponent(); }

        public void SetAccountId(long accountId) => _accountId = accountId;

        private async void OnVerifyClicked(object sender, EventArgs e)
        {
            var vaultPwd = VaultPasswordEntry.Text?.Trim();
            if (string.IsNullOrEmpty(vaultPwd))
            {
                StatusLabel.Text = "Please enter your vault password.";
                StatusLabel.IsVisible = true;
                return;
            }

            StatusLabel.IsVisible = false;
            try
            {
                var response = await ApiClient.Instance.PostAsJsonAsync(
                    "api/forgot-password/verify-vault",
                    new { AccountId = _accountId, VaultPassword = vaultPwd });

                if (response.IsSuccessStatusCode)
                {
                    Proceed?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    StatusLabel.Text = "Incorrect vault password. Please try again.";
                    StatusLabel.IsVisible = true;
                }
            }
            catch
            {
                StatusLabel.Text = "Could not connect to the server.";
                StatusLabel.IsVisible = true;
            }
        }

        private void OnTogglePassword(object sender, EventArgs e)
        {
            _isPasswordVisible = !_isPasswordVisible;
            VaultPasswordEntry.IsPassword = !_isPasswordVisible;
            ToggleLabel.Text = _isPasswordVisible ? "\ue8f5" : "\ue8f4";
        }

        private void OnBackTapped(object sender, EventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);

        public void Reset()
        {
            VaultPasswordEntry.Text = string.Empty;
            StatusLabel.IsVisible = false;
            _isPasswordVisible = false;
            VaultPasswordEntry.IsPassword = true;
            ToggleLabel.Text = "\ue8f4";
        }

        private class ErrorResponse { public string Message { get; set; } = string.Empty; }
    }
}
