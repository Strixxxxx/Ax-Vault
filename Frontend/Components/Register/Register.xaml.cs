using System.Text.RegularExpressions;
using System.Net.Http.Json;

namespace Frontend.Components.Register;

public partial class Register : ContentView
{
    // Events
    public event EventHandler<bool>? RegistrationCompleted;
    public event EventHandler? LoginRequested;
    
    // Registration data
    private string? _username;
    private string? _email;
    private string? _password;
    private string? _uniqueKey;
    
    // Step components
    private readonly UsernameStep _usernameStep;
    private readonly EmailStep _emailStep;
    private readonly PasswordStep _passwordStep;
    
    // Current step tracker
    private int _currentStep = 1;
    
    public Register()
    {
        InitializeComponent();
        
        // Initialize the step components
        _usernameStep = new UsernameStep();
        _emailStep = new EmailStep();
        _passwordStep = new PasswordStep();
        
        // Subscribe to validation events
        _usernameStep.ValidationChanged += OnStepValidationChanged;
        _emailStep.ValidationChanged += OnStepValidationChanged;
        _passwordStep.ValidationChanged += OnStepValidationChanged;
        
        // Start with the first step
        ShowStep(1);
    }
    
    private void ShowStep(int step)
    {
        // Update current step
        _currentStep = step;
        
        // Update step indicators
        StepIndicator1.BackgroundColor = step >= 1 ? Color.FromArgb("#00e5ff") : Color.FromArgb("#333333");
        StepIndicator2.BackgroundColor = step >= 2 ? Color.FromArgb("#00e5ff") : Color.FromArgb("#333333");
        StepIndicator3.BackgroundColor = step >= 3 ? Color.FromArgb("#00e5ff") : Color.FromArgb("#333333");
        
        // Clear content area
        ContentArea.Children.Clear();
        
        // Add appropriate step
        switch (step)
        {
            case 1:
                ContentArea.Children.Add(_usernameStep);
                NextButton.Text = "Next";
                BackButton.IsEnabled = false;
                break;
                
            case 2:
                ContentArea.Children.Add(_emailStep);
                NextButton.Text = "Next";
                BackButton.IsEnabled = true;
                break;
                
            case 3:
                ContentArea.Children.Add(_passwordStep);
                NextButton.Text = "Register";
                BackButton.IsEnabled = true;
                break;
        }
        
        // Update Next button state based on current step's validation
        UpdateNextButtonState();
    }
    
    private void OnStepValidationChanged(object? sender, bool isValid)
    {
        UpdateNextButtonState();
    }
    
    private void UpdateNextButtonState()
    {
        switch (_currentStep)
        {
            case 1:
                NextButton.IsEnabled = _usernameStep.IsValid;
                break;
                
            case 2:
                NextButton.IsEnabled = _emailStep.IsValid;
                break;
                
            case 3:
                NextButton.IsEnabled = _passwordStep.IsValid;
                break;
        }
    }
    
    private void OnNextClicked(object? sender, EventArgs e)
    {
        // Save data from current step
        switch (_currentStep)
        {
            case 1:
                _username = _usernameStep.Username;
                ShowStep(2);
                break;
                
            case 2:
                _email = _emailStep.Email;
                ShowStep(3);
                break;
                
            case 3:
                _password = _passwordStep.Password;
                _uniqueKey = _passwordStep.UniqueKey;
                CompleteRegistration();
                break;
        }
    }
    
    private void OnBackClicked(object? sender, EventArgs e)
    {
        if (_currentStep > 1)
        {
            ShowStep(_currentStep - 1);
        }
    }
    
    private void OnLoginTapped(object? sender, EventArgs e)
    {
        LoginRequested?.Invoke(this, EventArgs.Empty);
    }
    
    private async void CompleteRegistration()
    {
        try
        {
            // Disable buttons during registration
            NextButton.IsEnabled = false;
            BackButton.IsEnabled = false;
            NextButton.Text = "Registering...";
            
            // Get the local timezone ID
            var timezone = TimeZoneInfo.Local.Id;

            // Prepare registration data for API call
            var registrationData = new
            {
                Username = _username,
                Email = _email,
                Password = _password,
                UniqueKey = _uniqueKey,
                Timezone = timezone
            };
            
            // Use the shared ApiClient instance to ensure the secret header is included
            var response = await Services.ApiClient.Instance.PostAsJsonAsync("api/auth/register", registrationData);
            
            if (response.IsSuccessStatusCode)
            {
                // Registration successful
                var result = await response.Content.ReadFromJsonAsync<RegistrationResponse>();
                
                // Show success message
                if (this.Window?.Page != null)
                    await this.Window.Page.DisplayAlert("Registration Successful", 
                        "Your account has been created. Please log in with your credentials.", "OK");
                
                // Signal successful registration
                RegistrationCompleted?.Invoke(this, false);
                
                // Request switch to login view
                LoginRequested?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                // Registration failed: read the raw error content from the backend.
                var errorDetails = await response.Content.ReadAsStringAsync();
                
                // Display the detailed error message from the backend.
                if (this.Window?.Page != null)
                    await this.Window.Page.DisplayAlert("Registration Error", 
                        string.IsNullOrWhiteSpace(errorDetails) ? "An unknown error occurred." : errorDetails,
                        "OK");

                RegistrationCompleted?.Invoke(this, false);
            }
        }
        catch (Exception ex)
        {
            // Show detailed exception message
            if (this.Window?.Page != null)
                await this.Window.Page.DisplayAlert("Registration Error", $"An unexpected error occurred: {ex.Message}", "OK");
            RegistrationCompleted?.Invoke(this, false);
        }
        finally
        {
            // Re-enable buttons
            NextButton.IsEnabled = true;
            BackButton.IsEnabled = true;
            NextButton.Text = "Register";
        }
    }
    
    // Method to reset the form to initial state
    public void ResetForm()
    {
        _username = null;
        _email = null;
        _password = null;
        _uniqueKey = null;
        
        _usernameStep.Reset();
        _emailStep.Reset();
        _passwordStep.Reset();
        
        ShowStep(1);
    }
    
    // Simple class to receive token response
    private class RegistrationResponse
    {
        public string? Message { get; set; }
        public string? Token { get; set; }
    }
} 