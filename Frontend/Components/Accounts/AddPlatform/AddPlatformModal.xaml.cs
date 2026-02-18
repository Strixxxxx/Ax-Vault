using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Frontend.Services;
using Frontend.Components.Toasts; // Added

namespace Frontend.Components.Accounts.AddPlatform
{
    public partial class AddPlatformModal : ContentPage
    {
        // Event to notify parent that platform was created
        public event EventHandler<bool>? PlatformCreated;

        public AddPlatformModal()
        {
            InitializeComponent();
            
            CancelButton.Clicked += OnCancelButtonClicked;
            CreateButton.Clicked += OnCreateButtonClicked;
        }



        private async void OnCreateButtonClicked(object? sender, EventArgs e)
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(PlatformNameEntry.Text))
            {
                ToastService.ShowToast("Platform name is required.", ToastType.Error);
                return;
            }
            
            // Check if user is logged in
            if (!SessionService.Instance.IsLoggedIn)
            {
                ToastService.ShowToast("User not authenticated. Please log in again to create platforms.", ToastType.Error);
                await Navigation.PopModalAsync(); // Close the modal since user can't proceed
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
                    ToastService.ShowToast("Your new platform has been successfully created!", ToastType.Success);
                    PlatformCreated?.Invoke(this, true);
                    await Navigation.PopModalAsync(); // Close the modal
                }
                else
                {
                    // Display detailed error message
                    ToastService.ShowToast($"Failed to add platform: {response.ErrorMessage}", ToastType.Error);
                }
            }
            catch (Exception ex)
            {
                ToastService.ShowToast($"An error occurred: {ex.Message}", ToastType.Error);
            }
        }

        private async Task<PlatformResponse> CreatePlatformAsync(PlatformModalModel platform)
        {
            try
            {
                var json = JsonSerializer.Serialize(platform);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                string? token = SessionService.Instance.AuthToken;
                string? username = SessionService.Instance.Username;

                if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(username))
                {
                    return new PlatformResponse { IsSuccess = false, ErrorMessage = "You are not logged in." };
                }

                // Create HTTP request
                var request = new HttpRequestMessage(HttpMethod.Post, "api/platform");
                request.Content = content;
                request.Headers.Add("X-Username", username);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                
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
                        Dispatcher.Dispatch(() => { 
                            try {
                                this.IsBusy = false;
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
            public string? Message { get; set; }
        }
    }

    // Model for platform creation (with no description field)
    public class PlatformModalModel
    {
        public string? Name { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
 