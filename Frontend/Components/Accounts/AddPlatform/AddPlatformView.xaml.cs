using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Frontend.Services;

namespace Frontend.Components.Accounts.AddPlatform
{
    public partial class AddPlatformView : ContentPage
    {
        private readonly string _username;

        public AddPlatformView()
        {
            InitializeComponent();
            
            // Get the username from local storage
            _username = GetUsernameFromLocalStorage();

            CancelButton.Clicked += OnCancelButtonClicked;
            CreateButton.Clicked += OnCreateButtonClicked;
        }

        private string GetUsernameFromLocalStorage()
        {
            // Retrieve the username from secure storage
            string username = Microsoft.Maui.Storage.Preferences.Get("Username", string.Empty);
            
            if (string.IsNullOrEmpty(username))
            {
                // No fallback - just return empty
                // An error will be shown when trying to create the platform
                return string.Empty;
            }
            
            return username;
        }

        private async void OnCreateButtonClicked(object sender, EventArgs e)
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(PlatformNameEntry.Text))
            {
                await DisplayAlert("Validation Error", "Platform name is required.", "OK");
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
                // Create the platform model
                var platform = new PlatformModel
                {
                    Name = PlatformNameEntry.Text,
                    Description = DescriptionEditor.Text ?? string.Empty,
                    CreatedAt = DateTime.Now // This will use the device's time zone, should be adjusted on the server
                };

                // Send the request to create a platform
                var response = await CreatePlatformAsync(platform);
                
                if (response)
                {
                    await DisplayAlert("Success", "Platform added successfully.", "OK");
                    await Navigation.PopAsync(); // Return to previous page
                }
                else
                {
                    await DisplayAlert("Error", "Failed to add platform. Please try again.", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"An error occurred: {ex.Message}", "OK");
            }
        }

        private async Task<bool> CreatePlatformAsync(PlatformModel platform)
        {
            try
            {
                var json = JsonSerializer.Serialize(platform);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                // Add username to headers
                var request = new HttpRequestMessage(HttpMethod.Post, "api/platform");
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

        private async void OnCancelButtonClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync(); // Return to previous page
        }
    }

    // Model for platform creation
    public class PlatformModel
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
 