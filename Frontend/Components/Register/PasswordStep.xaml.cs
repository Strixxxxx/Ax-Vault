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
    
    // Password validation state
    private bool _hasValidLength;
    private bool _hasUppercase;
    private bool _hasLowercase;
    private bool _hasDigit;
    private bool _hasSpecialChar;
    private bool _passwordsMatch;

    public bool IsValid => _hasValidLength && _hasUppercase && _hasLowercase && 
                          _hasDigit && _hasSpecialChar && _passwordsMatch;
    
    public string Password => PasswordEntry.Text;
    
    public PasswordStep()
    {
        InitializeComponent();
    }
    
    private void OnPasswordChanged(object? sender, TextChangedEventArgs e)
    {
        string password = e.NewTextValue ?? string.Empty;
        
        // Check length requirement (8-16 characters)
        _hasValidLength = password.Length >= 8 && password.Length <= 16;
        LengthIndicator.BackgroundColor = _hasValidLength ? Color.FromRgba("#00e5ff") : Color.FromRgba("#333333");
        
        // Check for uppercase letter
        _hasUppercase = _uppercasePattern.IsMatch(password);
        UppercaseIndicator.BackgroundColor = _hasUppercase ? Color.FromRgba("#00e5ff") : Color.FromRgba("#333333");
        
        // Check for lowercase letter
        _hasLowercase = _lowercasePattern.IsMatch(password);
        LowercaseIndicator.BackgroundColor = _hasLowercase ? Color.FromRgba("#00e5ff") : Color.FromRgba("#333333");
        
        // Check for digit
        _hasDigit = _digitPattern.IsMatch(password);
        DigitIndicator.BackgroundColor = _hasDigit ? Color.FromRgba("#00e5ff") : Color.FromRgba("#333333");
        
        // Check for special character
        _hasSpecialChar = _specialCharPattern.IsMatch(password);
        SpecialCharIndicator.BackgroundColor = _hasSpecialChar ? Color.FromRgba("#00e5ff") : Color.FromRgba("#333333");
        
        // Check if passwords match
        CheckPasswordsMatch();
        
        // Notify parent of validation change
        ValidationChanged?.Invoke(this, IsValid);
    }
    
    private void OnConfirmPasswordChanged(object? sender, TextChangedEventArgs e)
    {
        CheckPasswordsMatch();
        
        // Notify parent of validation change
        ValidationChanged?.Invoke(this, IsValid);
    }
    
    private void CheckPasswordsMatch()
    {
        string password = PasswordEntry.Text ?? string.Empty;
        string confirmPassword = ConfirmPasswordEntry.Text ?? string.Empty;
        
        _passwordsMatch = !string.IsNullOrEmpty(password) && password == confirmPassword;
        MatchIndicator.BackgroundColor = _passwordsMatch ? Color.FromRgba("#00e5ff") : Color.FromRgba("#333333");
    }
    
    public void Reset()
    {
        PasswordEntry.Text = string.Empty;
        ConfirmPasswordEntry.Text = string.Empty;
        
        _hasValidLength = false;
        _hasUppercase = false;
        _hasLowercase = false;
        _hasDigit = false;
        _hasSpecialChar = false;
        _passwordsMatch = false;
        
        LengthIndicator.BackgroundColor = Color.FromRgba("#333333");
        UppercaseIndicator.BackgroundColor = Color.FromRgba("#333333");
        LowercaseIndicator.BackgroundColor = Color.FromRgba("#333333");
        DigitIndicator.BackgroundColor = Color.FromRgba("#333333");
        SpecialCharIndicator.BackgroundColor = Color.FromRgba("#333333");
        MatchIndicator.BackgroundColor = Color.FromRgba("#333333");
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
    
    private void UpdateToggleIcon(Button toggleButton, bool isPassword)
    {
        var fontImageSource = (FontImageSource)toggleButton.ImageSource;
        // &#xe8f4; = visibility, &#xe8f5; = visibility_off
        fontImageSource.Glyph = isPassword ? "\ue8f4" : "\ue8f5";
        fontImageSource.Color = isPassword ? Color.FromRgba("#7f7f7f") : Color.FromRgba("#00e5ff");
    }
} 