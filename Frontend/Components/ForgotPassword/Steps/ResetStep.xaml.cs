using System.Net.Http.Json;
using Frontend.Services;

namespace Frontend.Components.ForgotPassword.Steps
{
    public partial class ResetStep : ContentView
    {
        public event EventHandler? Proceed;
        public event EventHandler? BackRequested;

        private long _accountId;
        private bool _newVisible = false;
        private bool _confirmVisible = false;

        public ResetStep() { InitializeComponent(); }

        public void SetAccountId(long accountId) => _accountId = accountId;

        private void OnPasswordChanged(object sender, TextChangedEventArgs e)
        {
            var pwd = e.NewTextValue ?? string.Empty;
            ReqLength.TextColor  = (pwd.Length >= 9 && pwd.Length <= 16)      ? Color.FromArgb("#00e5ff") : Color.FromArgb("#888888");
            ReqUpper.TextColor   = pwd.Any(char.IsUpper)                      ? Color.FromArgb("#00e5ff") : Color.FromArgb("#888888");
            ReqLower.TextColor   = pwd.Any(char.IsLower)                      ? Color.FromArgb("#00e5ff") : Color.FromArgb("#888888");
            ReqNumber.TextColor  = pwd.Any(char.IsDigit)                      ? Color.FromArgb("#00e5ff") : Color.FromArgb("#888888");
            ReqSpecial.TextColor = pwd.Any(c => !char.IsLetterOrDigit(c))     ? Color.FromArgb("#00e5ff") : Color.FromArgb("#888888");
        }

        private async void OnResetClicked(object sender, EventArgs e)
        {
            var newPwd     = NewPasswordEntry.Text ?? string.Empty;
            var confirmPwd = ConfirmPasswordEntry.Text ?? string.Empty;

            if (newPwd != confirmPwd)
            {
                StatusLabel.Text = "Passwords do not match.";
                StatusLabel.IsVisible = true;
                return;
            }

            // Client-side validation
            if (newPwd.Length < 9 || newPwd.Length > 16)           { ShowError("Password must be 9–16 characters."); return; }
            if (!newPwd.Any(char.IsUpper))                          { ShowError("Must contain at least one uppercase letter."); return; }
            if (!newPwd.Any(char.IsLower))                          { ShowError("Must contain at least one lowercase letter."); return; }
            if (!newPwd.Any(char.IsDigit))                          { ShowError("Must contain at least one number."); return; }
            if (!newPwd.Any(c => !char.IsLetterOrDigit(c)))         { ShowError("Must contain at least one special character."); return; }

            StatusLabel.IsVisible = false;
            try
            {
                var response = await ApiClient.Instance.PostAsJsonAsync(
                    "api/forgot-password/reset-password",
                    new { AccountId = _accountId, NewPassword = newPwd });

                if (response.IsSuccessStatusCode)
                {
                    Proceed?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    var err = await response.Content.ReadFromJsonAsync<ErrorResponse>();
                    ShowError(err?.Message ?? "Failed to reset password.");
                }
            }
            catch
            {
                ShowError("Could not connect to the server.");
            }
        }

        private void ShowError(string msg) { StatusLabel.Text = msg; StatusLabel.IsVisible = true; }

        private void OnToggleNew(object sender, EventArgs e)
        {
            _newVisible = !_newVisible;
            NewPasswordEntry.IsPassword = !_newVisible;
            ToggleNew.Text = _newVisible ? "\ue8f5" : "\ue8f4";
        }

        private void OnToggleConfirm(object sender, EventArgs e)
        {
            _confirmVisible = !_confirmVisible;
            ConfirmPasswordEntry.IsPassword = !_confirmVisible;
            ToggleConfirm.Text = _confirmVisible ? "\ue8f5" : "\ue8f4";
        }

        private void OnBackTapped(object sender, EventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);

        public void Reset()
        {
            NewPasswordEntry.Text = string.Empty;
            ConfirmPasswordEntry.Text = string.Empty;
            StatusLabel.IsVisible = false;
            _newVisible = false; _confirmVisible = false;
            NewPasswordEntry.IsPassword = true;
            ConfirmPasswordEntry.IsPassword = true;
            ToggleNew.Text = "\ue8f4";
            ToggleConfirm.Text = "\ue8f4";
        }

        private class ErrorResponse { public string Message { get; set; } = string.Empty; }
    }
}
