using System.Collections.ObjectModel;
using Microsoft.Maui.Controls;
using System;
using System.Linq;
using Microsoft.Maui.Graphics;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Controls.Shapes;
using Frontend.Services;

namespace Frontend.Components.Accounts
{
    public partial class AccountsView : ContentView
    {
        // Defining colors as constants
        private static readonly Color NeonBlueColor = Color.FromRgb(0, 229, 255); // #00e5ff
        private static readonly Color DarkBackgroundColor = Color.FromRgb(26, 26, 26); // #1a1a1a
        private static readonly Color BorderColor = Color.FromRgb(51, 51, 51); // #333333
        
        public ObservableCollection<AccountItem> Accounts { get; set; } = new ObservableCollection<AccountItem>();
        public ObservableCollection<PlatformItem> Platforms { get; set; } = new ObservableCollection<PlatformItem>();
        private AccountItem? selectedAccount;
        private string currentMode = "normal"; // normal, add, edit, delete
        private readonly string _username;
        private readonly string _databaseName;

        public AccountsView()
        {
            InitializeComponent();

            // Initialize event handlers
            AddButton.Clicked += OnAddButtonClicked;
            EditButton.Clicked += OnEditButtonClicked;
            DeleteButton.Clicked += OnDeleteButtonClicked;
            SearchEntry.TextChanged += OnSearchTextChanged;

            // Get username and database name from secure storage
            _username = SecureStorage.GetAsync("username").Result ?? string.Empty;
            _databaseName = SecureStorage.GetAsync("database_name").Result ?? string.Empty;

            // Initially disable edit and delete buttons until an account is selected
            UpdateButtonStates();
            
            // Load platforms and accounts when the view is initialized
            LoadPlatforms();
        }

        public async void LoadPlatforms()
        {
            try
            {
                // Clear existing platforms
                Platforms.Clear();
                
                // Log connection info and credentials
                Console.WriteLine("=== Starting LoadPlatforms ===");
                Console.WriteLine($"Username from SecureStorage: {_username}");
                Console.WriteLine($"Database name from SecureStorage: {_databaseName}");
                Console.WriteLine($"API base URL: {ApiClient.Instance.BaseAddress}");
                
                // Create a new request message to add custom headers per-request
                var request = new HttpRequestMessage(HttpMethod.Get, "api/platform");
                request.Headers.Add("X-Username", _username);
                request.Headers.Add("X-Database-Name", _databaseName);
                
                // Log the request details
                Console.WriteLine("\n=== Making API Request ===");
                Console.WriteLine($"GET {ApiClient.Instance.BaseAddress}api/platform");
                Console.WriteLine("Headers:");
                foreach (var header in request.Headers)
                {
                    Console.WriteLine($"  {header.Key}: {string.Join(", ", header.Value)}");
                }
                
                // Fetch platforms from the API using the singleton ApiClient
                var response = await ApiClient.Instance.SendAsync(request);
                
                Console.WriteLine("\n=== API Response ===");
                Console.WriteLine($"Status Code: {response.StatusCode}");
                Console.WriteLine($"Reason Phrase: {response.ReasonPhrase}");
                
                var content = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Response Content: {content}");
                
                if (response.IsSuccessStatusCode)
                {
                    var platforms = await response.Content.ReadFromJsonAsync<List<PlatformApiModel>>();
                    
                    Console.WriteLine($"\n=== Processing Response ===");
                    Console.WriteLine($"Received {platforms?.Count ?? 0} platforms from API");
                    
                    if (platforms != null && platforms.Any())
                    {
                        foreach (var platform in platforms)
                        {
                            Console.WriteLine($"Adding platform: {platform.Name} with {platform.AccountCount} accounts");
                            Platforms.Add(new PlatformItem { 
                                Name = platform.Name, 
                                AccountCount = platform.AccountCount 
                            });
                        }
                        
                        // Update "No accounts" label visibility
                        NoAccountsLabel.IsVisible = false;
                    }
                    else
                    {
                        Console.WriteLine("No platforms found in the database.");
                        NoAccountsLabel.IsVisible = true;
                        NoAccountsLabel.Text = "No platforms found. Click 'Add' to create your first platform.";
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Error fetching platforms: {response.StatusCode}, {errorContent}");
                    
                    // Show error message in the UI
                    NoAccountsLabel.IsVisible = true;
                    NoAccountsLabel.Text = "Could not fetch platforms. Please check your connection.";
                }
                
                Console.WriteLine("\n=== Rendering Platforms ===");
                // Render platforms in normal mode
                RenderPlatformsNormalMode();
                Console.WriteLine("=== LoadPlatforms Complete ===\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n=== ERROR in LoadPlatforms ===");
                Console.WriteLine($"Error Message: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                
                // Show error message in the UI
                NoAccountsLabel.IsVisible = true;
                NoAccountsLabel.Text = $"Error connecting to server. Please try again later.";
            }
        }

        private void RenderPlatformsNormalMode()
        {
            // Clear existing account items
            AccountsGrid.Clear();
            AccountsGrid.RowDefinitions.Clear();
            
            // Hide the "no accounts" label if we have platforms
            NoAccountsLabel.IsVisible = Platforms.Count == 0;
            
            if (Platforms.Count == 0)
                return;
                
            // Calculate rows needed (divide by 2 and round up)
            int rowCount = (Platforms.Count + 1) / 2;
            
            // Create row definitions
            for (int i = 0; i < rowCount; i++)
            {
                AccountsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }
            
            // Add the platform items to the grid
            for (int i = 0; i < Platforms.Count; i++)
            {
                var platform = Platforms[i];
                int row = i / 2;
                int col = i % 2;
                
                var platformFrame = CreatePlatformFrame(platform);
                AccountsGrid.Add(platformFrame, col, row);
            }
        }
        
        private Border CreatePlatformFrame(PlatformItem platform)
        {
            // Create a tap gesture recognizer
            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += (s, e) => OnPlatformSelected(platform);
            
            // Create the platform card
            var border = new Border
            {
                BackgroundColor = DarkBackgroundColor,
                Stroke = BorderColor,
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(5) },
                Padding = new Thickness(15),
                Margin = new Thickness(0, 0, 0, 15),
                BindingContext = platform
            };
            border.GestureRecognizers.Add(tapGesture);
            
            // Create grid for platform icon and name
            var grid = new Grid
            {
                ColumnDefinitions = 
                {
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Star }
                }
            };
            
            // Platform icon (folder)
            var iconLabel = new Label
            {
                Text = "📁",
                FontSize = 32,
                VerticalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 0, 15, 0)
            };
            
            // Platform name and account count
            var infoStack = new VerticalStackLayout
            {
                Spacing = 4
            };
            
            // Platform name
            infoStack.Children.Add(new Label
            {
                Text = platform.Name,
                FontAttributes = FontAttributes.Bold,
                TextColor = NeonBlueColor,
                FontSize = 18
            });
            
            // Account count
            infoStack.Children.Add(new Label
            {
                Text = $"{platform.AccountCount} {(platform.AccountCount == 1 ? "account" : "accounts")}",
                TextColor = Colors.LightGray,
                FontSize = 14
            });
            
            // Add elements to grid
            grid.Add(iconLabel, 0, 0);
            grid.Add(infoStack, 1, 0);
            
            // Add action button if in add/edit/delete mode
            if (currentMode != "normal")
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                
                Button actionButton = null;
                
                switch (currentMode)
                {
                    case "add":
                        actionButton = new Button
                        {
                            Text = "Add Account",
                            BackgroundColor = NeonBlueColor,
                            TextColor = Colors.Black,
                            CornerRadius = 5,
                            FontSize = 14,
                            BindingContext = platform,
                            HorizontalOptions = LayoutOptions.End
                        };
                        actionButton.Clicked += OnAddAccountButtonClicked;
                        break;
                    case "edit":
                        actionButton = new Button
                        {
                            Text = "Edit Account",
                            BackgroundColor = NeonBlueColor,
                            TextColor = Colors.Black,
                            CornerRadius = 5,
                            FontSize = 14,
                            BindingContext = platform,
                            HorizontalOptions = LayoutOptions.End
                        };
                        actionButton.Clicked += OnEditAccountButtonClicked;
                        break;
                    case "delete":
                        actionButton = new Button
                        {
                            Text = "Delete Account",
                            BackgroundColor = NeonBlueColor,
                            TextColor = Colors.Black,
                            CornerRadius = 5,
                            FontSize = 14,
                            BindingContext = platform,
                            HorizontalOptions = LayoutOptions.End
                        };
                        actionButton.Clicked += OnDeleteAccountButtonClicked;
                        break;
                }
                
                if (actionButton != null)
                {
                    grid.Add(actionButton, 2, 0);
                }
            }
            
            border.Content = grid;
            return border;
        }

        private void OnPlatformSelected(PlatformItem platform)
        {
            // This would typically navigate to show the accounts for this platform
            // For now, we'll just highlight the selected platform
            foreach (var child in AccountsGrid.Children)
            {
                if (child is Border itemBorder)
                {
                    if (itemBorder.BindingContext is PlatformItem p && p.Name == platform.Name)
                    {
                        itemBorder.Stroke = NeonBlueColor;
                    }
                    else
                    {
                        itemBorder.Stroke = BorderColor;
                    }
                }
            }
        }

        private void UpdateButtonStates()
        {
            // Reset button text
            switch (currentMode)
            {
                case "normal":
                    AddButton.Text = "Add";
                    EditButton.Text = "Edit";
                    DeleteButton.Text = "Delete";
                    break;
                case "add":
                    AddButton.Text = "Add Platform";
                    EditButton.IsEnabled = false;
                    DeleteButton.IsEnabled = false;
                    break;
                case "edit":
                    AddButton.Text = "Add";
                    EditButton.Text = "Cancel";
                    DeleteButton.Text = "Delete";
                    AddButton.IsEnabled = false;
                    AddButton.Opacity = 0.5;
                    DeleteButton.IsEnabled = false;
                    DeleteButton.Opacity = 0.5;
                    break;
                case "delete":
                    AddButton.Text = "Add";
                    EditButton.Text = "Edit";
                    DeleteButton.Text = "Cancel";
                    AddButton.IsEnabled = false;
                    AddButton.Opacity = 0.5;
                    EditButton.IsEnabled = false;
                    EditButton.Opacity = 0.5;
                    break;
            }
        }

        private void OnAddButtonClicked(object sender, EventArgs e)
        {
            if (currentMode == "normal")
            {
                // Change mode to add
                currentMode = "add";
                
                // Update button states
                AddButton.Text = "Add Platform";
                EditButton.IsEnabled = false;
                DeleteButton.IsEnabled = false;
                
                // Render platforms with "Add Account" buttons
                RenderPlatformsInActionMode();
            }
            else
            {
                // User clicked "Add Platform" button while in add mode
                // Show the add platform modal
                ShowAddPlatformModal();
            }
        }
        
        private void RenderPlatformsInActionMode()
        {
            // Re-render platforms with action buttons
            RenderPlatformsNormalMode();
        }

        private void OnAddAccountButtonClicked(object sender, EventArgs e)
        {
            if (sender is Button button && button.BindingContext is PlatformItem platform)
            {
                // Navigate to the Add Account page with the platform name
                // For now, just show an alert
                Application.Current.MainPage.DisplayAlert("Add Account", $"Adding account to {platform.Name}", "OK");
            }
        }
        
        private void OnEditAccountButtonClicked(object sender, EventArgs e)
        {
            if (sender is Button button && button.BindingContext is PlatformItem platform)
            {
                // Navigate to the Edit Account page with the platform name
                // For now, just show an alert
                Application.Current.MainPage.DisplayAlert("Edit Account", $"Editing account in {platform.Name}", "OK");
            }
        }
        
        private void OnDeleteAccountButtonClicked(object sender, EventArgs e)
        {
            if (sender is Button button && button.BindingContext is PlatformItem platform)
            {
                // Show confirmation dialog and delete the account
                // For now, just show an alert
                Application.Current.MainPage.DisplayAlert("Delete Account", $"Deleting account from {platform.Name}", "OK");
            }
        }
        
        private async void ShowAddPlatformModal()
        {
            var addPlatformModal = new AddPlatform.AddPlatformModal();
            
            // Subscribe to the platform created event
            addPlatformModal.PlatformCreated += (sender, success) =>
            {
                if (success)
                {
                    // Reload platforms after a new one is created
                    LoadPlatforms();
                    
                    // Reset mode to normal
                    currentMode = "normal";
                    UpdateButtonStates();
                }
            };
            
            // Show the modal
            await Application.Current.MainPage.Navigation.PushModalAsync(addPlatformModal);
        }

        private void OnEditButtonClicked(object sender, EventArgs e)
        {
            if (currentMode == "normal")
            {
                // Switch to edit mode
                currentMode = "edit";
                UpdateButtonStates();
                RenderPlatformsInActionMode();
            }
            else if (currentMode == "edit")
            {
                // Switch back to normal mode
                currentMode = "normal";
                UpdateButtonStates();
                RenderPlatformsNormalMode();
            }
        }

        private void OnDeleteButtonClicked(object sender, EventArgs e)
        {
            if (currentMode == "normal")
            {
                // Switch to delete mode
                currentMode = "delete";
                UpdateButtonStates();
                RenderPlatformsInActionMode();
            }
            else if (currentMode == "delete")
            {
                // Switch back to normal mode
                currentMode = "normal";
                UpdateButtonStates();
                RenderPlatformsNormalMode();
            }
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = e.NewTextValue?.ToLower() ?? string.Empty;
            
            if (string.IsNullOrWhiteSpace(searchText))
            {
                // If search is empty, show all platforms
                if (currentMode == "normal")
                {
                    RenderPlatformsNormalMode();
                }
                else
                {
                    RenderPlatformsInActionMode();
                }
            }
            else
            {
                // Filter platforms based on search text
                var filteredPlatforms = Platforms
                    .Where(p => p.Name.ToLower().Contains(searchText))
                    .ToList();
                
                // Clear existing items
                AccountsGrid.Clear();
                AccountsGrid.RowDefinitions.Clear();
                
                // Show "no platforms" message if no results found
                NoAccountsLabel.IsVisible = filteredPlatforms.Count == 0;
                if (NoAccountsLabel.IsVisible)
                {
                    NoAccountsLabel.Text = $"No platforms found matching '{searchText}'.";
                }
                
                if (filteredPlatforms.Count == 0)
                    return;
                    
                // Calculate rows needed (divide by 2 and round up)
                int rowCount = (filteredPlatforms.Count + 1) / 2;
                
                // Create row definitions
                for (int i = 0; i < rowCount; i++)
                {
                    AccountsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                }
                
                // Add the filtered platform items to the grid
                for (int i = 0; i < filteredPlatforms.Count; i++)
                {
                    var platform = filteredPlatforms[i];
                    int row = i / 2;
                    int col = i % 2;
                    
                    var platformFrame = CreatePlatformFrame(platform);
                    AccountsGrid.Add(platformFrame, col, row);
                }
            }
        }

        // This class represents a platform item in the UI
        public class PlatformItem
        {
            public string Name { get; set; }
            public int AccountCount { get; set; }
        }

        // This class represents an account item in the UI
        public class AccountItem
        {
            public string Id { get; set; }
            public string Platform { get; set; }
            public string Username { get; set; }
            public string Password { get; set; }
            public DateTime Created { get; set; }
            public DateTime LastModified { get; set; }
        }

        // Add this class to represent the platform data returned from the API
        public class PlatformApiModel
        {
            public string Name { get; set; } = string.Empty;
            public int AccountCount { get; set; }
        }
    }
}