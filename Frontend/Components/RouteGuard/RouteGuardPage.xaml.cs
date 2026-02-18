using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace Frontend.Components.RouteGuard
{
    public partial class RouteGuardPage : ContentPage
    {
        private readonly RouteGuardService _routeGuardService;
        private readonly string _username;
        private readonly string? _token; // In-memory token
        private readonly string _targetModule;
        private readonly Action _onSuccessCallback;
        private readonly Action _onCancelCallback;
        
        public string TargetModule { get; private set; }
        public string ErrorMessage { get; private set; } = string.Empty;
        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
        
        // Password visibility properties
        public bool IsPasswordHidden { get; private set; } = true;
        // Material icons for visibility toggle: "visibility" and "visibility_off"
        public string TogglePasswordIcon => IsPasswordHidden ? "\uE8F4" : "\uE8F5";
        
        // Loading state
        public bool IsNotBusy => !IsBusy;
        
        public RouteGuardPage(string username, string targetModule, Action onSuccessCallback, Action onCancelCallback, string? token = null)
        {
            InitializeComponent();
            
            _routeGuardService = new RouteGuardService();
            
            // Store parameters
            _username = username;
            _token = token;
            _targetModule = targetModule;
            _onSuccessCallback = onSuccessCallback;
            _onCancelCallback = onCancelCallback;
            
            // Set binding context
            TargetModule = targetModule;
            BindingContext = this;
        }
        
        private void OnTogglePasswordVisibility(object sender, EventArgs e)
        {
            IsPasswordHidden = !IsPasswordHidden;
            OnPropertyChanged(nameof(IsPasswordHidden));
            OnPropertyChanged(nameof(TogglePasswordIcon));
        }
        
        private async void OnVerifyClicked(object sender, EventArgs e)
        {
            try
            {
                string vaultPassword = VaultPasswordEntry.Text;
                
                if (string.IsNullOrEmpty(vaultPassword))
                {
                    ErrorMessage = "Please enter your vault password";
                    OnPropertyChanged(nameof(ErrorMessage));
                    OnPropertyChanged(nameof(HasError));
                    return;
                }
                
                // Show loading indicator
                IsBusy = true;
                OnPropertyChanged(nameof(IsBusy));
                OnPropertyChanged(nameof(IsNotBusy));
                
                // Add a small delay to show loading state
                await Task.Delay(500);
                
                // Validate access with the backend
                bool isAuthorized = await _routeGuardService.ValidateModuleAccess(_username, vaultPassword, _targetModule, _token);
                
                if (isAuthorized)
                {
                    // Update session service with the vault password (Zero-Knowledge v2)
                    Frontend.Services.SessionService.Instance.VaultPassword = vaultPassword;

                    // Access granted - continue to the module
                    _onSuccessCallback?.Invoke();
                    await Navigation.PopModalAsync();
                }
                else
                {
                    // Access denied - show error
                    ErrorMessage = "Invalid vault password. Please try again";
                    OnPropertyChanged(nameof(ErrorMessage));
                    OnPropertyChanged(nameof(HasError));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception in RouteGuardPage: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                ErrorMessage = $"An error occurred: {ex.Message}";
                OnPropertyChanged(nameof(ErrorMessage));
                OnPropertyChanged(nameof(HasError));
            }
            finally
            {
                IsBusy = false;
                OnPropertyChanged(nameof(IsBusy));
                OnPropertyChanged(nameof(IsNotBusy));
            }
        }
        
        private async void OnCancelClicked(object sender, EventArgs e)
        {
            _onCancelCallback?.Invoke();
            await Navigation.PopModalAsync();
        }

        private void OnVaultPasswordEntryCompleted(object? sender, EventArgs e)
        {
            OnVerifyClicked(this, EventArgs.Empty);
        }
    }
} 