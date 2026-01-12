using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Frontend.Services;

namespace Frontend.Components.Accounts.AddAccount
{
    public partial class AddAccountView : ContentPage
    {
        private readonly string _platformName;
        private readonly string _username;
        private bool _showPassword = false;

        public AddAccountView(string platformName)
        {
            InitializeComponent();
            _platformName = platformName;
            
            // Get the username from local storage
            _username = GetUsernameFromLocalStorage();
            
            // Set the platform name in the UI
            PlatformNameLabel.Text = platformName;
            
            // Attach event handlers
            CancelButton.Clicked += OnCancelButtonClicked;
            CreateButton.Clicked += OnCreateButtonClicked;
            ShowPasswordButton.Clicked += OnShowPasswordButtonClicked;
        }
        
        private string GetUsernameFromLocalStorage()
        {
            // Retrieve the username from secure storage
            string username = Microsoft.Maui.Storage.Preferences.Get("Username", string.Empty);
            
            if (string.IsNullOrEmpty(username))
            {
                // No fallback - just return empty
                // An error will be shown when trying to create the account
                return string.Empty;
            }
            
            return username;
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
                await DisplayAlert("Validation Error", "Username is required.", "OK");
                return;
            }

            if (string.IsNullOrWhiteSpace(PasswordEntry.Text))
            {
                await DisplayAlert("Validation Error", "Password is required.", "OK");
                return;
            }
            
            // Check if username is available
            if (string.IsNullOrEmpty(_username))
            {
                await DisplayAlert("Authentication Error", "User not authenticated. Please log in again.", "OK");
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
                    await DisplayAlert("Success", "Account added successfully.", "OK");
                    await Navigation.PopAsync(); // Return to previous page
                }
                else
                {
                    await DisplayAlert("Error", "Failed to add account. Please try again.", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"An error occurred: {ex.Message}", "OK");
            }
        }

        private async Task<bool> CreateAccountAsync(AccountModel account)
        {
            try
            {
                var json = JsonSerializer.Serialize(account);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                // Add username to headers
                var request = new HttpRequestMessage(HttpMethod.Post, "api/account");
                request.Content = content;
                request.Headers.Add("X-Username", _username);
                
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
 