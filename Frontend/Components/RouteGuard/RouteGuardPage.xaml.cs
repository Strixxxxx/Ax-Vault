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
        
        public RouteGuardPage(string username, string targetModule, Action onSuccessCallback, Action onCancelCallback)
        {
            InitializeComponent();
            
            _routeGuardService = new RouteGuardService();
            
            // Store parameters
            _username = username;
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
                string uniqueKey = UniqueKeyEntry.Text;
                
                if (string.IsNullOrEmpty(uniqueKey))
                {
                    ErrorMessage = "Please enter your unique key";
                    OnPropertyChanged(nameof(ErrorMessage));
                    OnPropertyChanged(nameof(HasError));
                    return;
                }
                
                // Show loading indicator
                IsBusy = true;
                OnPropertyChanged(nameof(IsBusy));
                OnPropertyChanged(nameof(IsNotBusy));
                
                Console.WriteLine($"Attempting to validate with username: {_username}, key length: {uniqueKey.Length}, module: {_targetModule}");
                
                // Add a small delay to show loading state
                await Task.Delay(500);
                
                // Validate access with the backend
                bool isAuthorized = await _routeGuardService.ValidateModuleAccess(_username, uniqueKey, _targetModule);
                Console.WriteLine($"Validation result: {isAuthorized}");
                
                if (isAuthorized)
                {
                    // Access granted - continue to the module
                    Console.WriteLine("Access granted, proceeding to module");
                    _onSuccessCallback?.Invoke();
                    await Navigation.PopModalAsync();
                }
                else
                {
                    // Access denied - show error
                    Console.WriteLine("Access denied, showing error");
                    ErrorMessage = "Invalid unique key. Please try again.";
                    OnPropertyChanged(nameof(ErrorMessage));
                    OnPropertyChanged(nameof(HasError));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception in RouteGuardPage: {ex}");
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
    }
} 