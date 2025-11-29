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
        private string _username;
        private string _databaseName;
        private const int MaxRetries = 3;
        private const int RetryDelayMs = 1000;
        
        public DashboardView()
        {
            InitializeComponent();
            
            // Initialize collections
            _platformItems = new ObservableCollection<PlatformItem>();
            
            // Set binding context
            BindingContext = this;
        }
        
        public void Initialize(string username, string databaseName)
        {
            _username = username;
            _databaseName = databaseName;
            
            // Load data from the API
            LoadPlatformDataWithRetry();
        }
        
        private async void LoadPlatformDataWithRetry()
        {
            // Show loading indicator and hide error messages
            LoadingIndicator.IsVisible = true;
            LoadingIndicator.IsRunning = true;
            ErrorMessageLabel.IsVisible = false;
            NoPlatformsLabel.IsVisible = false;

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
                        await Application.Current.MainPage.Dispatcher.DispatchAsync(() =>
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
            }
        }
        
        private async Task LoadPlatformDataAsync()
        {
            // Clear any existing data
            PlatformItems.Clear();
            
            // Log connection info
            Console.WriteLine($"Loading platforms with username: {_username}, database: {_databaseName}");
            Console.WriteLine($"API base URL: {ApiClient.Instance.BaseAddress}");
            
            try
            {
                // Create a new request message to add custom per-request headers
                var request = new HttpRequestMessage(HttpMethod.Get, "api/platform");
                request.Headers.Add("X-Username", _username);
                request.Headers.Add("X-Database-Name", _databaseName);
                
                // Log the request
                Console.WriteLine($"Sending GET request to: {ApiClient.Instance.BaseAddress}api/platform");
                
                // Fetch platforms from the API
                var response = await ApiClient.Instance.SendAsync(request);
                
                Console.WriteLine($"API Response Status: {response.StatusCode}");
                
                if (response.IsSuccessStatusCode)
                {
                    var platforms = await response.Content.ReadFromJsonAsync<List<PlatformApiModel>>();
                    
                    Console.WriteLine($"Received {platforms?.Count ?? 0} platforms from API");
                    
                    if (platforms != null && platforms.Any())
                    {
                        foreach (var platform in platforms)
                        {
                            Console.WriteLine($"Adding platform: {platform.Name} with {platform.AccountCount} accounts");
                            PlatformItems.Add(new PlatformItem("📁", platform.Name, platform.AccountCount));
                        }
                        
                        // Hide loading and error indicators
                        await Application.Current.MainPage.Dispatcher.DispatchAsync(() =>
                        {
                            LoadingIndicator.IsVisible = false;
                            LoadingIndicator.IsRunning = false;
                            ErrorMessageLabel.IsVisible = false;
                            NoPlatformsLabel.IsVisible = false;
                        });
                    }
                    else
                    {
                        Console.WriteLine("No platforms found in the database.");
                        await Application.Current.MainPage.Dispatcher.DispatchAsync(() =>
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
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Error fetching platforms: {response.StatusCode}, {errorContent}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in LoadPlatformDataAsync: {ex.Message}");
                throw; // Rethrow to be handled by retry logic
            }
        }
        
        private void RemoveStaticCards()
        {
            // This method is no longer needed since we removed the static cards from the XAML
        }
    }
    
    public class PlatformItem
    {
        public string Icon { get; set; }
        public string Name { get; set; }
        public int AccountCount { get; set; }
        public ICommand ViewCommand { get; set; }
        
        public PlatformItem(string icon, string name, int accountCount)
        {
            Icon = icon;
            Name = name;
            AccountCount = accountCount;
            ViewCommand = new Command(ExecuteViewCommand);
        }
        
        private void ExecuteViewCommand()
        {
            // Navigate to accounts view for this platform
            // This would be implemented to navigate to the accounts page filtered by this platform
            Shell.Current.GoToAsync($"//accounts?platform={Uri.EscapeDataString(Name)}");
        }
    }

    // Add this class to represent the platform data returned from the API
    public class PlatformApiModel
    {
        public string Name { get; set; } = string.Empty;
        public int AccountCount { get; set; }
    }
}
 