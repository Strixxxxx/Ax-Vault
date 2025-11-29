using System.Text.RegularExpressions;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using Frontend.Services;

namespace Frontend.Components.Register;

public partial class EmailStep : ContentView
{
    // Event to notify parent of validation changes
    public event EventHandler<bool>? ValidationChanged;
    
    // Email validation pattern
    private readonly Regex _emailPattern = new Regex(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$");
    
    public bool IsValid { get; private set; }
    
    public string Email => EmailEntry.Text;
    
    // For API calls
    private CancellationTokenSource? _cancellationTokenSource;
    
    public EmailStep()
    {
        InitializeComponent();
    }
    
    private async void OnEmailChanged(object sender, TextChangedEventArgs e)
    {
        string email = e.NewTextValue?.Trim();
        
        if (string.IsNullOrEmpty(email))
        {
            ShowValidationError("Email address is required");
            return;
        }
        
        if (!_emailPattern.IsMatch(email))
        {
            ShowValidationError("Please enter a valid email address");
            return;
        }
        
        // Cancel any previous check
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _cancellationTokenSource.Token;
        
        try
        {
            // Show checking indicator
            ValidationMessage.Text = "Checking email availability...";
            ValidationMessage.TextColor = Colors.Gray;
            ValidationMessage.IsVisible = true;
            IsValid = false;
            ValidationChanged?.Invoke(this, false);
            
            // Add a small delay to avoid too many requests while typing
            await Task.Delay(500, cancellationToken);
            
            if (cancellationToken.IsCancellationRequested)
                return;
                
            // Check if email is available
            var response = await ApiClient.Instance.GetAsync($"api/auth/check-email?email={Uri.EscapeDataString(email)}", cancellationToken);
            
            if (cancellationToken.IsCancellationRequested)
                return;
                
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<EmailCheckResponse>(cancellationToken: cancellationToken);
                
                if (result.IsAvailable)
                {
                    // Email is valid and available
                    ValidationMessage.Text = "Email is available";
                    ValidationMessage.TextColor = Colors.Green;
                    ValidationMessage.IsVisible = true;
                    IsValid = true;
                    ValidationChanged?.Invoke(this, true);
                }
                else
                {
                    ShowValidationError("Email is already registered");
                }
            }
            else
            {
                // Error checking availability, but don't block the user
                ValidationMessage.IsVisible = false;
                IsValid = true;
                ValidationChanged?.Invoke(this, true);
            }
        }
        catch (OperationCanceledException)
        {
            // Request was canceled, do nothing
        }
        catch (Exception)
        {
            // Error checking availability, but don't block the user
            ValidationMessage.IsVisible = false;
            IsValid = true;
            ValidationChanged?.Invoke(this, true);
        }
    }
    
    private void ShowValidationError(string message)
    {
        ValidationMessage.Text = message;
        ValidationMessage.TextColor = Colors.Red;
        ValidationMessage.IsVisible = true;
        IsValid = false;
        ValidationChanged?.Invoke(this, false);
    }
    
    public void Reset()
    {
        EmailEntry.Text = string.Empty;
        ValidationMessage.IsVisible = false;
        IsValid = false;
        _cancellationTokenSource?.Cancel();
    }
    
    // Response model for email availability check
    private class EmailCheckResponse
    {
        public bool IsAvailable { get; set; }
    }
}
 