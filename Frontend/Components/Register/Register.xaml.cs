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
    private string? _vaultPassword;
    
    // Step components
    private readonly UsernameStep _usernameStep;
    private readonly EmailStep _emailStep;
    private readonly PasswordStep _passwordStep;
    private readonly VaultPasswordStep _vaultPasswordStep;
    
    // Current step tracker
    private int _currentStep = 1;
    
    public Register()
    {
        InitializeComponent();
        
        // Initialize the step components
        _usernameStep = new UsernameStep();
        _emailStep = new EmailStep();
        _passwordStep = new PasswordStep();
        _vaultPasswordStep = new VaultPasswordStep();
        
        // Subscribe to validation events
        _usernameStep.ValidationChanged += OnStepValidationChanged;
        _emailStep.ValidationChanged += OnStepValidationChanged;
        _passwordStep.ValidationChanged += OnStepValidationChanged;
        _vaultPasswordStep.ValidationChanged += OnStepValidationChanged;
        
        // Start with the first step
        ShowStep(1);
    }
    
    private void ShowStep(int step)
    {
        // Update current step
        _currentStep = step;
        
        // Update step indicators
        StepIndicator1.BackgroundColor = step >= 1 ? Color.FromRgba("#00e5ff") : Color.FromRgba("#333333");
        StepIndicator2.BackgroundColor = step >= 2 ? Color.FromRgba("#00e5ff") : Color.FromRgba("#333333");
        StepIndicator3.BackgroundColor = step >= 3 ? Color.FromRgba("#00e5ff") : Color.FromRgba("#333333");
        StepIndicator4.BackgroundColor = step >= 4 ? Color.FromRgba("#00e5ff") : Color.FromRgba("#333333");
        
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
                NextButton.Text = "Next";
                BackButton.IsEnabled = true;
                break;

            case 4:
                ContentArea.Children.Add(_vaultPasswordStep);
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

            case 4:
                NextButton.IsEnabled = _vaultPasswordStep.IsValid;
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
                ShowStep(4);
                break;

            case 4:
                _vaultPassword = _vaultPasswordStep.VaultPassword;
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

            // Prepare registration data for API call (Zero-Knowledge v2)
            var registrationData = new
            {
                Username = _username,
                Email = _email,
                Password = _password,
                VaultPassword = _vaultPassword,
                Timezone = timezone
            };
            
            // Use the shared ApiClient instance
            var response = await Services.ApiClient.Instance.PostAsJsonAsync("api/auth/register", registrationData);
            
            if (response.IsSuccessStatusCode)
            {
                // Show success message
                if (App.Current?.Windows[0].Page != null) // Changed
                    await App.Current!.Windows[0].Page!.DisplayAlertAsync("Registration Successful", // Changed
                        "Your account has been created. Please log in with your credentials.", "OK");
                
                // Signal successful registration
                RegistrationCompleted?.Invoke(this, false);
                
                // Request switch to login view
                LoginRequested?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                var errorDetails = await response.Content.ReadAsStringAsync();
                
                if (App.Current?.Windows[0].Page != null) // Changed
                    await App.Current!.Windows[0].Page!.DisplayAlertAsync("Registration Error", // Changed
                        string.IsNullOrWhiteSpace(errorDetails) ? "An unknown error occurred." : errorDetails,
                        "OK");

                RegistrationCompleted?.Invoke(this, false);
            }
        }
        catch (Exception ex)
        {
            if (App.Current?.Windows[0].Page != null) // Changed
                await App.Current!.Windows[0].Page!.DisplayAlertAsync("Registration Error", $"An unexpected error occurred: {ex.Message}", "OK"); // Changed
            RegistrationCompleted?.Invoke(this, false);
        }
        finally
        {
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
        _vaultPassword = null;
        
        _usernameStep.Reset();
        _emailStep.Reset();
        _passwordStep.Reset();
        _vaultPasswordStep.Reset();
        
        ShowStep(1);
    }
    
    private class RegistrationResponse
    {
        public string? Message { get; set; }
        public string? Token { get; set; }
    }
}