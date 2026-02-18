using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Frontend.Services;
using Frontend.Components.Toasts;

namespace Frontend.Components.Accounts.AddAccount
{
    public partial class AddAccountView : ContentPage
    {
        private readonly string _platformName;
        private bool _showPassword = false;

        public AddAccountView(string platformName)
        {
            InitializeComponent();
            _platformName = platformName;
            
            // Get the username from local storage
            // _username = GetUsernameFromLocalStorage(); // Removed this line
            
            // Set the platform name in the UI
            PlatformNameLabel.Text = platformName;
            
            // Attach event handlers
            CancelButton.Clicked += OnCancelButtonClicked;
            CreateButton.Clicked += OnCreateButtonClicked;
            ShowPasswordButton.Clicked += OnShowPasswordButtonClicked;
        }
        


        private void OnShowPasswordButtonClicked(object? sender, EventArgs e)
        {
            _showPassword = !_showPassword;
            PasswordEntry.IsPassword = !_showPassword;
            ShowPasswordButton.Text = _showPassword ? "🙈" : "👁️";
        }

        private async void OnCreateButtonClicked(object? sender, EventArgs e)
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(UsernameEntry.Text))
            {
                ToastService.ShowToast("Username is required.", ToastType.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(PasswordEntry.Text))
            {
                ToastService.ShowToast("Password is required.", ToastType.Error);
                return;
            }
            
            // Check if user is logged in
            if (!SessionService.Instance.IsLoggedIn)
            {
                ToastService.ShowToast("User not authenticated. Please log in again.", ToastType.Error);
                return;
            }

            try
            {
                // Create the account model
                var account = new AccountModel
                {
                    Platform = _platformName,
                    Username = UsernameEntry.Text,
                    Password = PasswordEntry.Text,
                    Description = DescriptionEditor.Text ?? string.Empty,
                    CreatedAt = DateTime.Now // This will use the device's time zone, should be adjusted on the server
                };

                // Send the request to create an account
                var response = await CreateAccountAsync(account);
                
                if (response)
                {
                    ToastService.ShowToast("Account added successfully.", ToastType.Success);
                    await Navigation.PopAsync(); // Return to previous page
                }
                else
                {
                    ToastService.ShowToast("Failed to add account. Please try again.", ToastType.Error);
                }
            }
            catch (Exception ex)
            {
                ToastService.ShowToast($"An error occurred: {ex.Message}", ToastType.Error);
            }
        }

        private async Task<bool> CreateAccountAsync(AccountModel account)
        {
            try
            {
                var json = JsonSerializer.Serialize(account);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                string? token = SessionService.Instance.AuthToken;
                string? username = SessionService.Instance.Username;

                if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(username))
                {
                    return false;
                }

                // Create HTTP request
                var request = new HttpRequestMessage(HttpMethod.Post, "api/account");
                request.Content = content;
                request.Headers.Add("X-Username", username);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                
                var response = await ApiClient.Instance.SendAsync(request);
                
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private async void OnCancelButtonClicked(object? sender, EventArgs e)
        {
            await Navigation.PopAsync(); // Return to previous page
        }
    }

    // Model for account creation
    public class AccountModel
    {
        public string? Platform { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
 