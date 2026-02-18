using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using Frontend.Components.RouteGuard;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Linq;
using Microsoft.Maui.Devices;
using Frontend.Services;
using Frontend.Models;
using Frontend.Components.Modals; // Added
using Frontend.Components.Layout; // Added

namespace Frontend.Components.Dashboard
{
    public partial class DashboardView : ContentView
    {
        private ObservableCollection<PlatformItem> _platformItems;
        public ObservableCollection<PlatformItem> PlatformItems 
        { 
            get => _platformItems;
            set
            {
                _platformItems = value;
                OnPropertyChanged(nameof(PlatformItems));
            }
        }
        private ObservableCollection<PlatformItem> _allPlatformItems = new ObservableCollection<PlatformItem>(); // Added for search functionality
        private string _username = string.Empty;
        private const int MaxRetries = 3;
        private const int RetryDelayMs = 1000;
        // private bool _isVaultPasswordPromptActive = false; // Removed to resolve CS0414 warning
        private MainLayout? _parentLayout; // Store reference to parent layout

        public DashboardView()
        {
            InitializeComponent();
            
            // Initialize collections
            _platformItems = new ObservableCollection<PlatformItem>();
            
            // Set binding context
            BindingContext = this;
        }
        
        public void Initialize(string username, MainLayout parentLayout)
        {
            _username = username; // Update username
            _parentLayout = parentLayout; // Store parent layout reference

            // Load data from the API
            _ = LoadPlatformDataWithRetry();
        }
        
        public async Task LoadPlatformDataWithRetry()
        {
            // Show loading indicator and hide error messages
            LoadingIndicator.IsVisible = true;
            LoadingIndicator.IsRunning = true;
            ErrorMessageLabel.IsVisible = false;
            NoPlatformsLabel.IsVisible = false;

            // Check if VaultPassword is set
            if (string.IsNullOrEmpty(SessionService.Instance.VaultPassword))
            {
                // Use RouteGuardNavigator for vault password verification
                var routeGuardNavigator = new RouteGuardNavigator(
                    Application.Current!.Windows[0].Page!.Navigation,
                    _username,
                    SessionService.Instance.AuthToken
                );

                // Define dummy page for navigation, as we only need the modal functionality
                var dummyPage = new ContentPage(); 

                await routeGuardNavigator.NavigateToAsync(dummyPage, "Dashboard");

                // After RouteGuardPage closes, check if vault password is now set
                if (!string.IsNullOrEmpty(SessionService.Instance.VaultPassword))
                {
                    await LoadPlatformDataWithRetry(); // Retry loading after password is set
                }
                else
                {
                    LoadingIndicator.IsVisible = false;
                    LoadingIndicator.IsRunning = false;
                    ErrorMessageLabel.Text = "Vault password entry cancelled or failed.";
                    ErrorMessageLabel.IsVisible = true;
                }
                return; 
            }

            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    await LoadPlatformDataAsync();
                    return; // Success, exit the retry loop
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Attempt {attempt} failed: {ex.Message}");
                    
                    if (attempt == MaxRetries)
                    {
                        await Dispatcher.DispatchAsync(() =>
                        {
                            ErrorMessageLabel.Text = "Could not connect to the server. Please ensure the backend service is running.";
                            ErrorMessageLabel.IsVisible = true;
                            LoadingIndicator.IsVisible = false;
                            LoadingIndicator.IsRunning = false;
                        });
                    }
                    else
                    {
                        // Wait before retrying with exponential backoff
                        await Task.Delay(RetryDelayMs * attempt * attempt);
                    }
                }
                catch (Exception ex)
                {
                    // Handle other exceptions during password verification or data loading
                    await Dispatcher.DispatchAsync(() =>
                    {
                        ErrorMessageLabel.Text = $"An unexpected error occurred: {ex.Message}";
                        ErrorMessageLabel.IsVisible = true;
                        LoadingIndicator.IsVisible = false;
                        LoadingIndicator.IsRunning = false;
                    });
                    return;
                }
            }
        }
        
        private async Task LoadPlatformDataAsync()
        {
            // Clear any existing data
            PlatformItems.Clear();
            _allPlatformItems.Clear(); // Clear all items for fresh load
            
            // Log connection info
            Console.WriteLine($"Loading platforms for user: {_username}");
            
            try
            {
                string? token = SessionService.Instance.AuthToken;
                string? username = SessionService.Instance.Username;
                string? vaultPassword = SessionService.Instance.VaultPassword; // Get VaultPassword

                if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(vaultPassword))
                {
                    PlatformItems.Clear();
                    _allPlatformItems.Clear();
                    await Dispatcher.DispatchAsync(() =>
                    {
                        LoadingIndicator.IsVisible = false;
                        LoadingIndicator.IsRunning = false;
                        NoPlatformsLabel.IsVisible = true;
                        NoPlatformsLabel.Text = "Please log in and enter vault password to view platforms and accounts.";
                    });
                    return;
                }

                // Create a new request message to get platforms
                var platformRequest = new HttpRequestMessage(HttpMethod.Get, "api/platforms");
                
                // Fetch platforms from the API
                var platformResponse = await ApiClient.Instance.SendAsync(platformRequest);
                
                if (platformResponse.IsSuccessStatusCode)
                {
                    var platforms = await platformResponse.Content.ReadFromJsonAsync<List<PlatformApiModel>>();
                    
                    if (platforms != null && platforms.Any())
                    {
                        foreach (var platform in platforms)
                        {
                            string displayName = FormatPlatformName(platform.Name);
                            var platformItem = new PlatformItem("📁", displayName, platform.AccountCount)
                            {
                                ViewAccountsCommand = new Command<PlatformItem>(async (p) => await OnPlatformSelected(p))
                            };
                            
                            _allPlatformItems.Add(platformItem); // Add to master list
                        }
                        
                        // Populate the displayed list from the master list initially
                        foreach (var item in _allPlatformItems)
                        {
                            PlatformItems.Add(item);
                        }

                        await Dispatcher.DispatchAsync(() =>
                        {
                            LoadingIndicator.IsVisible = false;
                            LoadingIndicator.IsRunning = false;
                            ErrorMessageLabel.IsVisible = false;
                            NoPlatformsLabel.IsVisible = false;
                        });
                    }
                    else
                    {
                        await Dispatcher.DispatchAsync(() =>
                        {
                            LoadingIndicator.IsVisible = false;
                            LoadingIndicator.IsRunning = false;
                            ErrorMessageLabel.IsVisible = false;
                            NoPlatformsLabel.IsVisible = true;
                        });
                    }
                }
                else
                {
                    var errorContent = await platformResponse.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Error fetching platforms: {platformResponse.StatusCode}, {errorContent}");
                }
            }

            catch (Exception ex)
            {
                Console.WriteLine($"Error in LoadPlatformDataAsync: {ex.Message}");
                throw;
            }
        }

        private async Task OnPlatformSelected(PlatformItem platform)
        {
            var modal = new DashboardAccountListModal(platform); // Corrected constructor
            _parentLayout?.ShowModal(modal); // Use parent layout's ShowModal
        }

        private string FormatPlatformName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;

            // The backend is now returning clean platform names from the Vaults table.
            // This method might not be needed anymore, or can be used for other formatting.
            return name;
        }
        
        private void OnSearchEntryTextChanged(object? sender, TextChangedEventArgs e)
        {
            var searchText = e.NewTextValue?.ToLowerInvariant() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(searchText))
            {
                // If search text is empty, show all items
                PlatformItems.Clear();
                foreach (var item in _allPlatformItems)
                {
                    PlatformItems.Add(item);
                }
            }
            else
            {
                // Filter items based on search text
                var filteredItems = _allPlatformItems.Where(p => p.Name.ToLowerInvariant().Contains(searchText)).ToList();
                PlatformItems.Clear();
                foreach (var item in filteredItems)
                {
                    PlatformItems.Add(item);
                }
            }
        }

        private void RemoveStaticCards()
        {
            // This method is no longer needed since we removed the static cards from the XAML
        }
    }
}
 