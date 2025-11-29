using System.Net.Http.Json;
using Frontend.Services;

namespace Frontend.Components.Login;

public partial class Login : ContentView
{
    public event EventHandler<bool>? LoginCompleted;
    public event EventHandler? RegisterRequested;

    public Login()
    {
        InitializeComponent();
    }
    
    private void OnTogglePasswordVisibility(object sender, EventArgs e)
    {
        // Toggle password visibility
        PasswordEntry.IsPassword = !PasswordEntry.IsPassword;
        
        // Update icon based on password visibility
        // &#xe8f4; = visibility (eye icon), &#xe8f5; = visibility_off (eye with slash)
        TogglePasswordLabel.Text = PasswordEntry.IsPassword ? "\ue8f4" : "\ue8f5";
        TogglePasswordLabel.TextColor = PasswordEntry.IsPassword ? Colors.White : Color.FromArgb("#00e5ff");
    }

    private async void OnLoginClicked(object sender, EventArgs e)
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
            var response = await ApiClient.Instance.PostAsJsonAsync("api/login", loginData);
            
            if (response.IsSuccessStatusCode)
            {
                // Login successful
                var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
                
                // Store the token securely
                await SecureStorage.SetAsync("auth_token", result.Token);
                
                // Store the username
                await SecureStorage.SetAsync("username", UsernameEntry.Text);
                
                // Store the database name if available
                if (!string.IsNullOrEmpty(result.DatabaseName))
                {
                    await SecureStorage.SetAsync("database_name", result.DatabaseName);
                    Console.WriteLine($"Stored database name in secure storage: {result.DatabaseName}");
                }

                // Store the timezone if available
                if (!string.IsNullOrEmpty(result.Timezone))
                {
                    await SecureStorage.SetAsync("timezone", result.Timezone);
                    Console.WriteLine($"Stored timezone in secure storage: {result.Timezone}");
                }
                
                // Signal successful login
                LoginCompleted?.Invoke(this, true);
            }
            else
            {
                // Login failed
                var errorDetails = await response.Content.ReadAsStringAsync();
                StatusLabel.Text = "Invalid username/email or password";
                StatusLabel.IsVisible = true;
                LoginCompleted?.Invoke(this, false);
            }
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Error: {ex.Message}";
            StatusLabel.IsVisible = true;
            LoginCompleted?.Invoke(this, false);
        }
        finally
        {
            LoginButton.IsEnabled = true;
            LoginButton.Text = "Sign In";
        }
    }

    private void OnRegisterTapped(object sender, EventArgs e)
    {
        // Trigger event to notify parent that user wants to register
        RegisterRequested?.Invoke(this, EventArgs.Empty);
    }

    // Simple class to receive token response
    private class LoginResponse
    {
        public string? Token { get; set; }
        public string? Username { get; set; }
        public string? Email { get; set; }
        public string? DatabaseName { get; set; }
        public string? Timezone { get; set; }
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