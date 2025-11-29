using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Frontend.Services;

namespace Frontend.Components.Accounts.AddPlatform
{
    public partial class AddPlatformModal : ContentPage
    {
        private string? _username;
        private string? _databaseName;
        
        // Event to notify parent that platform was created
        public event EventHandler<bool>? PlatformCreated;

        public AddPlatformModal()
        {
            InitializeComponent();
            
            CancelButton.Clicked += OnCancelButtonClicked;
            CreateButton.Clicked += OnCreateButtonClicked;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadAsync();
        }
        
        private async Task LoadAsync()
        {
            // Get the username and database name from secure storage properly with async/await
            _username = await SecureStorage.GetAsync("username");
            _databaseName = await SecureStorage.GetAsync("database_name");
            
            // Log that we retrieved the user info
            Console.WriteLine($"Retrieved username from secure storage: {(_username ?? "null")}");
            Console.WriteLine($"Retrieved database name from secure storage: {(_databaseName ?? "null")}");
        }

        private async void OnCreateButtonClicked(object? sender, EventArgs e)
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(PlatformNameEntry.Text))
            {
                await DisplayAlert("Validation Error", "Platform name is required.", "OK");
                return;
            }
            
            // Check if username is available - no fallback
            if (string.IsNullOrEmpty(_username))
            {
                await DisplayAlert("Authentication Error", "User not authenticated. Please log in again to create platforms.", "OK");
                await Navigation.PopModalAsync(); // Close the modal since user can't proceed
                return;
            }
            
            // Check if we have a database name
            if (string.IsNullOrEmpty(_databaseName))
            {
                await DisplayAlert("Configuration Error", "Database name not found. Please log out and log in again.", "OK");
                return;
            }

            try
            {
                // Create the platform model - no description field
                var platform = new PlatformModalModel
                {
                    Name = PlatformNameEntry.Text.Trim(), // Allow spaces in platform name
                    CreatedAt = DateTime.Now // This will use the device's time zone, should be adjusted on the server
                };

                // Send the request to create a platform
                var response = await CreatePlatformAsync(platform);
                
                if (response.IsSuccess)
                {
                    await DisplayAlert("Success", "Platform added successfully.", "OK");
                    PlatformCreated?.Invoke(this, true);
                    await Navigation.PopModalAsync(); // Close the modal
                }
                else
                {
                    // Display detailed error message
                    await DisplayAlert("Error", $"Failed to add platform: {response.ErrorMessage}", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"An error occurred: {ex.Message}", "OK");
            }
        }

        private async Task<PlatformResponse> CreatePlatformAsync(PlatformModalModel platform)
        {
            try
            {
                var json = JsonSerializer.Serialize(platform);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                // Add username and database_name to headers
                var request = new HttpRequestMessage(HttpMethod.Post, "api/platform");
                request.Content = content;
                request.Headers.Add("X-Username", _username);
                
                // Add database name to headers if available
                if (!string.IsNullOrEmpty(_databaseName))
                {
                    request.Headers.Add("X-Database-Name", _databaseName);
                    Console.WriteLine($"Added X-Database-Name header: {_databaseName}");
                }
                else
                {
                    Console.WriteLine("WARNING: No database name available to add to headers!");
                }
                
                // Add some diagnostic output
                Console.WriteLine($"Sending platform creation request - Username: {_username}, Database: {_databaseName}, Platform: {platform.Name}, URL: {ApiClient.Instance.BaseAddress}api/platform");
                
                // Display network activity indicator (if available on the platform)
                if (DeviceInfo.Platform == DevicePlatform.iOS)
                {
                    Application.Current.Dispatcher.Dispatch(() => { 
                        try {
                            Application.Current.MainPage.IsBusy = true;
                        } catch { /* ignore */ }
                    });
                }
                
                try
                {
                    var response = await ApiClient.Instance.SendAsync(request);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        return new PlatformResponse { IsSuccess = true };
                    }
                    else
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"Platform creation failed: Status code={response.StatusCode}, Content={errorContent}");
                        
                        // Try to parse the error message from JSON
                        string errorMessage = $"Server returned: {response.StatusCode}";
                        try
                        {
                            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                            var errorObj = JsonSerializer.Deserialize<ErrorResponse>(errorContent, options);
                            if (!string.IsNullOrEmpty(errorObj?.Message))
                            {
                                errorMessage = errorObj.Message;
                            }
                            else
                            {
                                errorMessage = errorContent;
                            }
                        }
                        catch (Exception ex)
                        {
                            // If we can't parse the JSON, just use the raw error content
                            Console.WriteLine($"Error parsing JSON response: {ex.Message}");
                            errorMessage = errorContent;
                        }
                        
                        return new PlatformResponse { 
                            IsSuccess = false, 
                            ErrorMessage = errorMessage
                        };
                    }
                }
                finally
                {
                    // Hide network activity indicator
                    if (DeviceInfo.Platform == DevicePlatform.iOS)
                    {
                        Application.Current.Dispatcher.Dispatch(() => { 
                            try {
                                Application.Current.MainPage.IsBusy = false;
                            } catch { /* ignore */ }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Platform creation exception: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                return new PlatformResponse { 
                    IsSuccess = false, 
                    ErrorMessage = ex.Message 
                };
            }
        }

        private async void OnCancelButtonClicked(object? sender, EventArgs e)
        {
            await Navigation.PopModalAsync(); // Close the modal
        }
        
        // Helper classes for response handling
        private class PlatformResponse
        {
            public bool IsSuccess { get; set; }
            public string ErrorMessage { get; set; } = "";
        }
        
        private class ErrorResponse
        {
            public string Message { get; set; }
        }
    }

    // Model for platform creation (with no description field)
    public class PlatformModalModel
    {
        public string? Name { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
 