using System.Text.RegularExpressions;

namespace Frontend.Components.Register;

public partial class PasswordStep : ContentView
{
    // Event to notify parent of validation changes
    public event EventHandler<bool>? ValidationChanged;
    
    // Password validation patterns
    private readonly Regex _uppercasePattern = new Regex(@"[A-Z]");
    private readonly Regex _lowercasePattern = new Regex(@"[a-z]");
    private readonly Regex _digitPattern = new Regex(@"[0-9]");
    private readonly Regex _specialCharPattern = new Regex(@"[^a-zA-Z0-9\s]");
    
    // Unique key validation pattern (alphanumeric, min 6 chars)
    private readonly Regex _uniqueKeyPattern = new Regex(@"^.{6,}$");
    
    // Validation flags
    private bool _hasValidLength;
    private bool _hasUppercase;
    private bool _hasLowercase;
    private bool _hasDigit;
    private bool _hasSpecialChar;
    private bool _passwordsMatch;
    private bool _hasValidUniqueKey;
    
    public bool IsValid => _hasValidLength && _hasUppercase && _hasLowercase && 
                          _hasDigit && _hasSpecialChar && _passwordsMatch && _hasValidUniqueKey;
    
    public string Password => PasswordEntry.Text;
    public string UniqueKey => UniqueKeyEntry.Text;
    
    public PasswordStep()
    {
        InitializeComponent();
    }
    
    private void OnPasswordChanged(object? sender, TextChangedEventArgs e)
    {
        string password = e.NewTextValue ?? string.Empty;
        string uniqueKey = UniqueKeyEntry.Text ?? string.Empty;
        
        // Check length requirement (8-16 characters)
        _hasValidLength = password.Length >= 8 && password.Length <= 16;
        LengthIndicator.BackgroundColor = _hasValidLength ? Color.FromArgb("#00e5ff") : Color.FromArgb("#333333");
        
        // Check for uppercase letter
        _hasUppercase = _uppercasePattern.IsMatch(password);
        UppercaseIndicator.BackgroundColor = _hasUppercase ? Color.FromArgb("#00e5ff") : Color.FromArgb("#333333");
        
        // Check for lowercase letter
        _hasLowercase = _lowercasePattern.IsMatch(password);
        LowercaseIndicator.BackgroundColor = _hasLowercase ? Color.FromArgb("#00e5ff") : Color.FromArgb("#333333");
        
        // Check for digit
        _hasDigit = _digitPattern.IsMatch(password);
        DigitIndicator.BackgroundColor = _hasDigit ? Color.FromArgb("#00e5ff") : Color.FromArgb("#333333");
        
        // Check for special character
        _hasSpecialChar = _specialCharPattern.IsMatch(password);
        SpecialCharIndicator.BackgroundColor = _hasSpecialChar ? Color.FromArgb("#00e5ff") : Color.FromArgb("#333333");
        
        // Check if passwords match
        CheckPasswordsMatch();
        
        // Also validate unique key - if it matches the password, mark it as invalid
        if (!string.IsNullOrEmpty(uniqueKey) && uniqueKey.Equals(password, StringComparison.Ordinal))
        {
            _hasValidUniqueKey = false;
            UniqueKeyIndicator.BackgroundColor = Color.FromArgb("#333333");
            KeyValidationMessage.Text = "Unique key must be different from your password";
            KeyValidationMessage.IsVisible = true;
        }
        else if (!string.IsNullOrEmpty(uniqueKey) && uniqueKey.Length >= 6 && _uniqueKeyPattern.IsMatch(uniqueKey))
        {
            _hasValidUniqueKey = true;
            UniqueKeyIndicator.BackgroundColor = Color.FromArgb("#00e5ff");
            KeyValidationMessage.IsVisible = false;
        }
        
        // Notify parent of validation change
        ValidationChanged?.Invoke(this, IsValid);
    }
    
    private void OnConfirmPasswordChanged(object? sender, TextChangedEventArgs e)
    {
        CheckPasswordsMatch();
        
        // Notify parent of validation change
        ValidationChanged?.Invoke(this, IsValid);
    }
    
    private void OnUniqueKeyChanged(object? sender, TextChangedEventArgs e)
    {
        string uniqueKey = e.NewTextValue?.Trim() ?? string.Empty;
        string password = PasswordEntry.Text ?? string.Empty;
        
        if (string.IsNullOrEmpty(uniqueKey))
        {
            ShowKeyValidationError("Unique key is required");
            return;
        }
        
        if (uniqueKey.Length < 6)
        {
            ShowKeyValidationError("Unique key must be at least 6 characters");
            return;
        }
        
        if (!_uniqueKeyPattern.IsMatch(uniqueKey))
        {
            ShowKeyValidationError("Unique key must be at least 6 characters");
            return;
        }

        if (password.Equals(uniqueKey, StringComparison.Ordinal))
        {
            ShowKeyValidationError("Unique key must be different from your password");
            return;
        }
        
        // Unique key is valid
        KeyValidationMessage.IsVisible = false;
        _hasValidUniqueKey = true;
        UniqueKeyIndicator.BackgroundColor = Color.FromArgb("#00e5ff");
        
        // Notify parent of validation change
        ValidationChanged?.Invoke(this, IsValid);
    }
    
    private void ShowKeyValidationError(string message)
    {
        KeyValidationMessage.Text = message;
        KeyValidationMessage.IsVisible = true;
        _hasValidUniqueKey = false;
        UniqueKeyIndicator.BackgroundColor = Color.FromArgb("#333333");
        
        // Notify parent of validation change
        ValidationChanged?.Invoke(this, IsValid);
    }
    
    private void CheckPasswordsMatch()
    {
        string password = PasswordEntry.Text ?? string.Empty;
        string confirmPassword = ConfirmPasswordEntry.Text ?? string.Empty;
        string uniqueKey = UniqueKeyEntry.Text ?? string.Empty;
        
        _passwordsMatch = !string.IsNullOrEmpty(password) && password == confirmPassword;
        MatchIndicator.BackgroundColor = _passwordsMatch ? Color.FromArgb("#00e5ff") : Color.FromArgb("#333333");
        
        // Also check if unique key matches password when passwords match
        if (_passwordsMatch && !string.IsNullOrEmpty(uniqueKey) && uniqueKey.Equals(password, StringComparison.Ordinal))
        {
            _hasValidUniqueKey = false;
            UniqueKeyIndicator.BackgroundColor = Color.FromArgb("#333333");
            KeyValidationMessage.Text = "Unique key must be different from your password";
            KeyValidationMessage.IsVisible = true;
        }
    }
    
    public void Reset()
    {
        PasswordEntry.Text = string.Empty;
        ConfirmPasswordEntry.Text = string.Empty;
        UniqueKeyEntry.Text = string.Empty;
        
        _hasValidLength = false;
        _hasUppercase = false;
        _hasLowercase = false;
        _hasDigit = false;
        _hasSpecialChar = false;
        _passwordsMatch = false;
        _hasValidUniqueKey = false;
        
        LengthIndicator.BackgroundColor = Color.FromArgb("#333333");
        UppercaseIndicator.BackgroundColor = Color.FromArgb("#333333");
        LowercaseIndicator.BackgroundColor = Color.FromArgb("#333333");
        DigitIndicator.BackgroundColor = Color.FromArgb("#333333");
        SpecialCharIndicator.BackgroundColor = Color.FromArgb("#333333");
        MatchIndicator.BackgroundColor = Color.FromArgb("#333333");
        UniqueKeyIndicator.BackgroundColor = Color.FromArgb("#333333");
        
        KeyValidationMessage.IsVisible = false;
    }
    
    private void OnPasswordToggleClicked(object sender, EventArgs e)
    {
        PasswordEntry.IsPassword = !PasswordEntry.IsPassword;
        UpdateToggleIcon(PasswordToggle, PasswordEntry.IsPassword);
    }
    
    private void OnConfirmToggleClicked(object sender, EventArgs e)
    {
        ConfirmPasswordEntry.IsPassword = !ConfirmPasswordEntry.IsPassword;
        UpdateToggleIcon(ConfirmToggle, ConfirmPasswordEntry.IsPassword);
    }
    
    private void OnKeyToggleClicked(object sender, EventArgs e)
    {
        UniqueKeyEntry.IsPassword = !UniqueKeyEntry.IsPassword;
        UpdateToggleIcon(KeyToggle, UniqueKeyEntry.IsPassword);
    }
    
    private void UpdateToggleIcon(Button toggleButton, bool isPassword)
    {
        var fontImageSource = (FontImageSource)toggleButton.ImageSource;
        // &#xe8f4; = visibility, &#xe8f5; = visibility_off
        fontImageSource.Glyph = isPassword ? "\ue8f4" : "\ue8f5";
        fontImageSource.Color = isPassword ? Color.FromArgb("#7f7f7f") : Color.FromArgb("#00e5ff");
    }
} 