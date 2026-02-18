using System.Text.RegularExpressions;

namespace Frontend.Components.Register;

public partial class VaultPasswordStep : ContentView
{
    public event EventHandler<bool>? ValidationChanged;
    
    private readonly Regex _uppercasePattern = new Regex(@"[A-Z]");
    private readonly Regex _lowercasePattern = new Regex(@"[a-z]");
    private readonly Regex _digitPattern = new Regex(@"[0-9]");
    private readonly Regex _specialCharPattern = new Regex(@"[^a-zA-Z0-9\s]");
    
    private bool _hasValidLength;
    private bool _hasUppercase;
    private bool _hasLowercase;
    private bool _hasDigit;
    private bool _hasSpecialChar;
    private bool _keysMatch;
    
    public bool IsValid => _hasValidLength && _hasUppercase && _hasLowercase && 
                          _hasDigit && _hasSpecialChar && _keysMatch;
    
    public string VaultPassword => VaultPasswordEntry.Text;
    
    public VaultPasswordStep()
    {
        InitializeComponent();
    }
    
    private void OnVaultPasswordChanged(object? sender, TextChangedEventArgs e)
    {
        string key = e.NewTextValue ?? string.Empty;
        
        // Check length requirement (12-16 characters as per user rules)
        _hasValidLength = key.Length >= 12 && key.Length <= 16;
        LengthIndicator.BackgroundColor = _hasValidLength ? Color.FromRgba("#00e5ff") : Color.FromRgba("#333333");
        
        _hasUppercase = _uppercasePattern.IsMatch(key);
        UppercaseIndicator.BackgroundColor = _hasUppercase ? Color.FromRgba("#00e5ff") : Color.FromRgba("#333333");
        
        _hasLowercase = _lowercasePattern.IsMatch(key);
        LowercaseIndicator.BackgroundColor = _hasLowercase ? Color.FromRgba("#00e5ff") : Color.FromRgba("#333333");
        
        _hasDigit = _digitPattern.IsMatch(key);
        DigitIndicator.BackgroundColor = _hasDigit ? Color.FromRgba("#00e5ff") : Color.FromRgba("#333333");
        
        _hasSpecialChar = _specialCharPattern.IsMatch(key);
        SpecialCharIndicator.BackgroundColor = _hasSpecialChar ? Color.FromRgba("#00e5ff") : Color.FromRgba("#333333");
        
        CheckKeysMatch();
        ValidationChanged?.Invoke(this, IsValid);
    }
    
    private void OnConfirmVaultPasswordChanged(object? sender, TextChangedEventArgs e)
    {
        CheckKeysMatch();
        ValidationChanged?.Invoke(this, IsValid);
    }
    
    private void CheckKeysMatch()
    {
        string key = VaultPasswordEntry.Text ?? string.Empty;
        string confirmKey = ConfirmVaultPasswordEntry.Text ?? string.Empty;
        
        _keysMatch = !string.IsNullOrEmpty(key) && key == confirmKey;
        MatchIndicator.BackgroundColor = _keysMatch ? Color.FromRgba("#00e5ff") : Color.FromRgba("#333333");
    }
    
    public void Reset()
    {
        VaultPasswordEntry.Text = string.Empty;
        ConfirmVaultPasswordEntry.Text = string.Empty;
        
        _hasValidLength = false;
        _hasUppercase = false;
        _hasLowercase = false;
        _hasDigit = false;
        _hasSpecialChar = false;
        _keysMatch = false;
        
        LengthIndicator.BackgroundColor = Color.FromRgba("#333333");
        match_indicators_reset();
    }

    private void match_indicators_reset()
    {
        UppercaseIndicator.BackgroundColor = Color.FromRgba("#333333");
        LowercaseIndicator.BackgroundColor = Color.FromRgba("#333333");
        DigitIndicator.BackgroundColor = Color.FromRgba("#333333");
        SpecialCharIndicator.BackgroundColor = Color.FromRgba("#333333");
        MatchIndicator.BackgroundColor = Color.FromRgba("#333333");
    }
    
    private void OnVaultPasswordToggleClicked(object sender, EventArgs e)
    {
        VaultPasswordEntry.IsPassword = !VaultPasswordEntry.IsPassword;
        UpdateToggleIcon(VaultPasswordToggle, VaultPasswordEntry.IsPassword);
    }
    
    private void OnConfirmToggleClicked(object sender, EventArgs e)
    {
        ConfirmVaultPasswordEntry.IsPassword = !ConfirmVaultPasswordEntry.IsPassword;
        UpdateToggleIcon(ConfirmToggle, ConfirmVaultPasswordEntry.IsPassword);
    }
    
    private void UpdateToggleIcon(Button toggleButton, bool isPassword)
    {
        var fontImageSource = (FontImageSource)toggleButton.ImageSource;
        fontImageSource.Glyph = isPassword ? "\ue8f4" : "\ue8f5";
        fontImageSource.Color = isPassword ? Color.FromRgba("#7f7f7f") : Color.FromRgba("#00e5ff");
    }
}
