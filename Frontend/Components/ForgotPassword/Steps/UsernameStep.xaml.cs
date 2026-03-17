using System.Net.Http.Json;
using Frontend.Services;

namespace Frontend.Components.ForgotPassword.Steps
{
    public partial class UsernameStep : ContentView
    {
        public event EventHandler<long>? Proceed;
        public event EventHandler? BackRequested;

        public UsernameStep() { InitializeComponent(); }

        private async void OnContinueClicked(object sender, EventArgs e)
        {
            var username = UsernameEntry.Text?.Trim();
            if (string.IsNullOrEmpty(username))
            {
                StatusLabel.Text = "Please enter your username.";
                StatusLabel.IsVisible = true;
                return;
            }

            StatusLabel.IsVisible = false;
            try
            {
                var response = await ApiClient.Instance.PostAsJsonAsync(
                    "api/forgot-password/verify-username",
                    new { Username = username });

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<VerifyResponse>();
                    Proceed?.Invoke(this, result?.AccountId ?? 0);
                }
                else
                {
                    var err = await response.Content.ReadFromJsonAsync<ErrorResponse>();
                    StatusLabel.Text = err?.Message ?? "Username not found.";
                    StatusLabel.IsVisible = true;
                }
            }
            catch (Exception ex)
            {
                // Specifically handle connection timeouts (Render Cold Start)
                if (ex is TaskCanceledException || (ex is HttpRequestException && ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase)))
                {
                    StatusLabel.Text = "Render is currently restarting, please try again after few seconds.";
                }
                else
                {
                    StatusLabel.Text = $"Error: {ex.Message}";
                }
                
                StatusLabel.IsVisible = true;
            }
        }

        private void OnBackTapped(object sender, EventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);

        public void Reset()
        {
            UsernameEntry.Text = string.Empty;
            StatusLabel.IsVisible = false;
        }

        private class VerifyResponse { public long AccountId { get; set; } }
        private class ErrorResponse { public string Message { get; set; } = string.Empty; }
    }
}
