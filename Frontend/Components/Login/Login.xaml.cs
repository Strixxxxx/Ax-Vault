using System.Net.Http.Json;
using Frontend.Services;

namespace Frontend.Components.Login;

public partial class Login : ContentView
{
    // Custom event args to pass login data
    public class LoginSuccessEventArgs : EventArgs
    {
        public bool Success { get; set; }
        public string? Token { get; set; }
        public string? Username { get; set; }
    }

    public event EventHandler<LoginSuccessEventArgs>? LoginCompleted;
    public event EventHandler? RegisterRequested;

    public Login()
    {
        InitializeComponent();
    }
    
    private void OnTogglePasswordVisibility(object? sender, EventArgs e)
    {
        // Toggle password visibility
        PasswordEntry.IsPassword = !PasswordEntry.IsPassword;
        
        // Update icon based on password visibility
        // &#xe8f4; = visibility (eye icon), &#xe8f5; = visibility_off (eye with slash)
        TogglePasswordLabel.Text = PasswordEntry.IsPassword ? "\ue8f4" : "\ue8f5";
        TogglePasswordLabel.TextColor = PasswordEntry.IsPassword ? Colors.White : Color.FromArgb("#00e5ff");
    }



    private void OnStayLoggedInTapped(object? sender, EventArgs e)
    {
        StayLoggedInCheckbox.IsChecked = !StayLoggedInCheckbox.IsChecked;
    }

    private async void OnLoginClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(UsernameEntry.Text) || string.IsNullOrWhiteSpace(PasswordEntry.Text))
        {
            StatusLabel.Text = "Username/Email and password are required";
            StatusLabel.IsVisible = true;
            return;
        }

        try
        {
            StatusLabel.IsVisible = false;
            LoginButton.IsEnabled = false;
            LoginButton.Text = "Signing in...";

            // Create login credentials
            var loginData = new
            {
                Username = UsernameEntry.Text,
                Password = PasswordEntry.Text
            };

            // Call the backend API to authenticate using the centralized ApiClient
            var response = await ApiClient.Instance.PostAsJsonAsync("api/auth/login", loginData);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
                
                if (result != null && !string.IsNullOrEmpty(result.Token))
                {
                    // Only store securely if "Stay Logged In" is checked
                    if (StayLoggedInCheckbox.IsChecked)
                    {
                        await SecureStorage.SetAsync("auth_token", result.Token);
                        await SecureStorage.SetAsync("username", result.Username ?? UsernameEntry.Text);
                    }
                    else 
                    {
                        // Ensure storage is clear if they didn't check it (safety)
                        SecureStorage.Remove("auth_token");
                        SecureStorage.Remove("username");
                    }
                    
                    // Signal successful login with data
                    LoginCompleted?.Invoke(this, new LoginSuccessEventArgs 
                    { 
                        Success = true,
                        Token = result.Token,
                        Username = result.Username ?? UsernameEntry.Text
                    });
                }
                else
                {
                    StatusLabel.Text = "Invalid response from server.";
                    StatusLabel.IsVisible = true;
                    LoginCompleted?.Invoke(this, new LoginSuccessEventArgs { Success = false });
                }
            }
            else
            {
                // Login failed
                var errorDetails = await response.Content.ReadAsStringAsync();
                StatusLabel.Text = "Invalid username/email or password";
                StatusLabel.IsVisible = true;
                LoginCompleted?.Invoke(this, new LoginSuccessEventArgs { Success = false });
            }
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Error: {ex.Message}";
            StatusLabel.IsVisible = true;
            LoginCompleted?.Invoke(this, new LoginSuccessEventArgs { Success = false });
        }
        finally
        {
            LoginButton.IsEnabled = true;
            LoginButton.Text = "Sign In";
        }
    }

    private void OnRegisterTapped(object? sender, EventArgs e)
    {
        // Trigger event to notify parent that user wants to register
        RegisterRequested?.Invoke(this, EventArgs.Empty);
    }

    // Simple class to receive token response
    private class LoginResponse
    {
        public string? Token { get; set; }
        public string? Username { get; set; }
    }

    // Reset the login form
    public void ResetForm()
    {
        UsernameEntry.Text = string.Empty;
        PasswordEntry.Text = string.Empty;
        StatusLabel.IsVisible = false;
        
        // Reset password visibility to hidden
        PasswordEntry.IsPassword = true;
        TogglePasswordLabel.Text = "\ue8f4";
        TogglePasswordLabel.TextColor = Colors.White;
    }
}