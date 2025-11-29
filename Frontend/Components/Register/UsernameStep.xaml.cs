using System.Text.RegularExpressions;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using Frontend.Services;

namespace Frontend.Components.Register;

public partial class UsernameStep : ContentView
{
    // Event to notify parent of validation changes
    public event EventHandler<bool>? ValidationChanged;
    
    // Updated username validation pattern to allow special characters (with proper escaping)
    private readonly Regex _usernamePattern = new Regex(@"^[a-zA-Z0-9_!@#$%^&*()\-+=\[\]{}|\\;:'"",.<>/?~]{3,16}$");
    
    public bool IsValid { get; private set; }
    
    public string Username => UsernameEntry.Text;
    
    // For API calls
    private CancellationTokenSource? _cancellationTokenSource;
    
    public UsernameStep()
    {
        InitializeComponent();
    }
    
    private async void OnUsernameChanged(object sender, TextChangedEventArgs e)
    {
        string username = e.NewTextValue?.Trim();
        
        if (string.IsNullOrEmpty(username))
        {
            ShowValidationError("Username is required");
            return;
        }
        
        if (username.Length < 3)
        {
            ShowValidationError("Username must be at least 3 characters");
            return;
        }
        
        if (username.Length > 16)
        {
            ShowValidationError("Username cannot exceed 16 characters");
            return;
        }
        
        if (!_usernamePattern.IsMatch(username))
        {
            ShowValidationError("Username can only contain letters, numbers, underscores, and special characters");
            return;
        }
        
        // Cancel any previous check
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _cancellationTokenSource.Token;
        
        try
        {
            // Show checking indicator
            ValidationMessage.Text = "Checking username availability...";
            ValidationMessage.TextColor = Colors.Gray;
            ValidationMessage.IsVisible = true;
            IsValid = false;
            ValidationChanged?.Invoke(this, false);
            
            // Add a small delay to avoid too many requests while typing
            await Task.Delay(500, cancellationToken);
            
            if (cancellationToken.IsCancellationRequested)
                return;
                
            // Check if username is available
            var response = await ApiClient.Instance.GetAsync($"api/auth/check-username?username={Uri.EscapeDataString(username)}", cancellationToken);
            
            if (cancellationToken.IsCancellationRequested)
                return;
                
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<UsernameCheckResponse>(cancellationToken: cancellationToken);
                
                if (result.IsAvailable)
                {
                    // Username is valid and available
                    ValidationMessage.Text = "Username is available";
                    ValidationMessage.TextColor = Colors.Green;
                    ValidationMessage.IsVisible = true;
                    IsValid = true;
                    ValidationChanged?.Invoke(this, true);
                }
                else
                {
                    ShowValidationError("Username is already taken");
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
        UsernameEntry.Text = string.Empty;
        ValidationMessage.IsVisible = false;
        IsValid = false;
        _cancellationTokenSource?.Cancel();
    }
    
    // Response model for username availability check
    private class UsernameCheckResponse
    {
        public bool IsAvailable { get; set; }
    }
}
 