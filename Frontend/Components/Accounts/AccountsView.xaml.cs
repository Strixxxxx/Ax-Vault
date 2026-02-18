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
using Frontend.Components.Accounts.Modals;
using Frontend.Models;

namespace Frontend.Components.Accounts
{
    public partial class AccountsView : ContentView
    {
        // Defining colors as constants
        private static readonly Color NeonBlueColor = Color.FromRgb(0, 229, 255); // #00e5ff
        private static readonly Color NeonGreenColor = Color.FromRgb(57, 255, 20); // #39ff14
        private static readonly Color NeonRedColor = Color.FromRgb(255, 7, 58); // #ff073a
        private static readonly Color DarkBackgroundColor = Color.FromRgb(26, 26, 26); // #1a1a1a
        private static readonly Color BorderColor = Color.FromRgb(51, 51, 51); // #333333
        
        public ObservableCollection<PlatformItem> Platforms { get; set; } = new ObservableCollection<PlatformItem>();
        private bool _isInitialized = false;
        private bool _isLoading = false;
        private AccountDetailModal? _currentAccountDetailModal; // Added to hold reference

        public AccountsView()
        {
            InitializeComponent();

            SearchEntry.TextChanged += OnSearchTextChanged;

            Loaded += OnAccountsViewLoaded;

            // Trigger reload when the view becomes visible
            this.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(IsVisible) && IsVisible)
                {
                    LoadPlatforms();
                }
            };
        }

        private void OnAccountsViewLoaded(object? sender, EventArgs e)
        {
            if (_isInitialized)
            {
                return;
            }
            _isInitialized = true;

            // Load platforms when the view is initialized
            LoadPlatforms();
        }

        public async void LoadPlatforms()
        {
            if (_isLoading) return;
            _isLoading = true;

            try
            {
                string? token = SessionService.Instance.AuthToken;
                string? username = SessionService.Instance.Username;
                
                if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(username))
                {
                    Platforms.Clear(); // Only clear if we can't load
                    NoAccountsLabel.IsVisible = true;
                    NoAccountsLabel.Text = "You are not logged in.";
                    _isLoading = false;
                    return;
                }
                
                // Clear existing platforms ONLY after we know we are authorized
                Platforms.Clear();
                
                // Create a new request message
                var request = new HttpRequestMessage(HttpMethod.Get, "api/platforms");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                
                // Fetch platforms from the API
                var response = await ApiClient.Instance.SendAsync(request);
                
                if (response.IsSuccessStatusCode)
                {
                    var platforms = await response.Content.ReadFromJsonAsync<List<PlatformApiModel>>();
                    
                    if (platforms != null && platforms.Any())
                    {
                        foreach (var platform in platforms)
                        {
                            string displayName = FormatPlatformName(platform.Name);
                            Platforms.Add(new PlatformItem { 
                                Name = displayName, 
                                AccountCount = platform.AccountCount 
                            });
                        }
                        
                        // Update "No accounts" label visibility
                        NoAccountsLabel.IsVisible = false;
                    }
                    else
                    {
                        NoAccountsLabel.IsVisible = true;
                        NoAccountsLabel.Text = "No platforms found. Click 'Add' to create your first platform.";
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    
                    // Show error message in the UI
                    NoAccountsLabel.IsVisible = true;
                    NoAccountsLabel.Text = "Could not fetch platforms. Please check your connection.";
                }
                
                // Render platforms in normal mode
                RenderPlatformsNormalMode();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in LoadPlatforms: {ex.Message}");
                
                // Show error message in the UI
                NoAccountsLabel.IsVisible = true;
                NoAccountsLabel.Text = $"Error connecting to server. Please try again later.";
            }
            finally
            {
                _isLoading = false;
            }
        }

        private string FormatPlatformName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;

            // If name is like "1_Google", remove "1_"
            int underscoreIndex = name.IndexOf('_');
            if (underscoreIndex > 0 && underscoreIndex < name.Length - 1)
            {
                string prefix = name.Substring(0, underscoreIndex);
                if (long.TryParse(prefix, out _))
                {
                    return name.Substring(underscoreIndex + 1);
                }
            }

            return name;
        }

        private void RenderPlatformsNormalMode()
        {
            AccountsGrid.Clear();
            AccountsGrid.RowDefinitions.Clear();
            AccountsGrid.ColumnDefinitions.Clear();

            NoAccountsLabel.IsVisible = Platforms.Count == 0;
            if (Platforms.Count == 0)
            {
                NoAccountsLabel.Text = "No accounts found. Create your first platform to get started.";
                return;
            }

            bool isMobile = DeviceInfo.Idiom == DeviceIdiom.Phone;
            int columns = isMobile ? 1 : 2;

            for (int i = 0; i < columns; i++)
            {
                AccountsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            }

            int rowCount = (Platforms.Count + columns - 1) / columns;
            for (int i = 0; i < rowCount; i++)
            {
                AccountsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            for (int i = 0; i < Platforms.Count; i++)
            {
                var platform = Platforms[i];
                int row = i / columns;
                int col = i % columns;

                var platformFrame = CreatePlatformFrame(platform);
                AccountsGrid.Add(platformFrame, col, row);
            }
        }
        
        private Border CreatePlatformFrame(PlatformItem platform)
        {
            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += (s, e) => OnPlatformSelected(platform);
            
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
            
            var mainStack = new VerticalStackLayout { Spacing = 10 };

            // Top: Name and Info
            var topGrid = new Grid
            {
                ColumnDefinitions = 
                {
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Star }
                }
            };
            
            // Differentiates the tap gesture for mobile and desktop
            if (DeviceInfo.Idiom == DeviceIdiom.Phone)
            {
                topGrid.GestureRecognizers.Add(tapGesture);
            }
            else
            {
                border.GestureRecognizers.Add(tapGesture);
            }

            var iconLabel = new Label
            {
                Text = "📁",
                FontSize = 32,
                VerticalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 0, 15, 0)
            };
            
            var infoStack = new VerticalStackLayout { Spacing = 2 };
            infoStack.Children.Add(new Label
            {
                Text = platform.Name,
                FontAttributes = FontAttributes.Bold,
                TextColor = NeonBlueColor,
                FontSize = 18
            });
            
            infoStack.Children.Add(new Label
            {
                Text = $"{platform.AccountCount} accounts",
                TextColor = Colors.Gray,
                FontSize = 12
            });

            topGrid.Add(iconLabel, 0, 0);
            topGrid.Add(infoStack, 1, 0);

            // Bottom: Edit and Delete Labels
            var actionsStack = new HorizontalStackLayout { Spacing = 20, HorizontalOptions = LayoutOptions.End };
            
            var editLabel = new Label
            {
                Text = "✎ edit",
                TextColor = NeonGreenColor,
                FontSize = 14,
                VerticalOptions = LayoutOptions.Center
            };
            var editTap = new TapGestureRecognizer();
            editTap.Tapped += (s, e) => OnEditPlatformClicked(platform);
            editLabel.GestureRecognizers.Add(editTap);

            // Create a HorizontalStackLayout for the delete icon and text
            var deleteLayout = new HorizontalStackLayout { Spacing = 5, VerticalOptions = LayoutOptions.Center };
            var deleteIcon = new Image
            {
                Source = new FontImageSource
                {
                    FontFamily = "MaterialIcons",
                    Glyph = "\uE872", // Material Icon for delete (trash can)
                    Color = NeonRedColor,
                    Size = 14 // Match FontSize of the text
                },
                VerticalOptions = LayoutOptions.Center
            };
            var deleteText = new Label
            {
                Text = "delete",
                TextColor = NeonRedColor,
                FontSize = 14,
                VerticalOptions = LayoutOptions.Center
            };
            deleteLayout.Children.Add(deleteIcon);
            deleteLayout.Children.Add(deleteText);

            var deleteTap = new TapGestureRecognizer();
            deleteTap.Tapped += (s, e) => OnDeletePlatformClicked(platform);
            deleteLayout.GestureRecognizers.Add(deleteTap);

            actionsStack.Children.Add(editLabel);
            actionsStack.Children.Add(deleteLayout); // Add the combined layout for delete

            mainStack.Children.Add(topGrid);
            mainStack.Children.Add(actionsStack);
            
            border.Content = mainStack;
            return border;
        }

        private void ShowModal(ContentView modalContent)
        {
            ModalPlaceholder.Children.Clear();
            ModalPlaceholder.Children.Add(modalContent);
            ModalContainer.IsVisible = true;
            
            // If it's a modal that has CloseRequested, hook it
            if (modalContent is PlatformModal pm) pm.CloseRequested += (s, e) => HideModal();
            if (modalContent is DeletePlatformModal dpm) dpm.CloseRequested += (s, e) => HideModal();
            if (modalContent is AccountDetailModal adm)
            {
                adm.CloseRequested += (s, e) => HideModal();
                adm.FormRequested += (s, e) => ShowAccountForm(e.Platform, e.VaultPassword, e.Account);
            }
            if (modalContent is AccountFormModal afm) afm.CloseRequested += (s, e) => 
            {
                HideModal();
                // If we came from detail, we might want to go back, but for now just close is safer/simpler
            };
        }

        private void HideModal()
        {
            ModalContainer.IsVisible = false;
            ModalPlaceholder.Children.Clear();
        }

        private void OnModalBackgroundTapped(object? sender, EventArgs e)
        {
            // Optional: Hide modal when clicking background
            // HideModal(); 
        }

        private async void OnPlatformSelected(PlatformItem platform)
        {
            if (DeviceInfo.Idiom == DeviceIdiom.Phone)
            {
                await Shell.Current.Navigation.PushAsync(new Pages.MobileAccountListPage(platform));
            }
            else
            {
                var detailModal = new AccountDetailModal(platform.Name, SessionService.Instance.VaultPassword);
                _currentAccountDetailModal = detailModal; // Store the reference
                ShowModal(detailModal);
            }
        }


        private void OnAddPlatformClicked(object? sender, EventArgs e)
        {
            var modal = new PlatformModal(true);
            modal.PlatformsUpdated += (s, ev) => LoadPlatforms();
            ShowModal(modal);
        }

        private async void OnEditPlatformClicked(PlatformItem platform)
        {
            if (DeviceInfo.Idiom == DeviceIdiom.Phone)
            {
                await Shell.Current.Navigation.PushAsync(new Pages.MobileEditPlatformPage(platform.Name));
            }
            else
            {
                var modal = new PlatformModal(false, platform.Name);
                modal.PlatformsUpdated += (s, ev) => LoadPlatforms();
                ShowModal(modal);
            }
        }

        private async void OnDeletePlatformClicked(PlatformItem platform)
        {
            if (DeviceInfo.Idiom == DeviceIdiom.Phone)
            {
                await Shell.Current.Navigation.PushAsync(new Pages.MobileDeletePlatformPage(platform.Name));
            }
            else
            {
                var modal = new DeletePlatformModal(platform.Name);
                modal.PlatformsUpdated += (s, ev) => LoadPlatforms();
                ShowModal(modal);
            }
        }

        private void ShowAccountForm(string platform, string vaultPassword, AccountResponseModel? account = null)
        {
            var formModal = new AccountFormModal(platform, vaultPassword, account);
            formModal.AccountUpdated += async (s, ev) => 
            {
                // Refresh the list of platforms to update account counts
                LoadPlatforms();
                
                // Also refresh the currently open AccountDetailModal if it exists
                if (_currentAccountDetailModal != null)
                {
                    await _currentAccountDetailModal.LoadAccounts();
                }
                
                HideModal();
                // If we came from detail, we might want to go back, but for now just close is safer/simpler
            };
            ShowModal(formModal);
        }

        private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
        {
            string searchText = e.NewTextValue?.ToLower() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(searchText))
            {
                RenderPlatformsNormalMode();
                return;
            }
            
            var filteredPlatforms = Platforms
                .Where(p => p.Name.ToLower().Contains(searchText))
                .ToList();

            AccountsGrid.Clear();
            AccountsGrid.RowDefinitions.Clear();
            AccountsGrid.ColumnDefinitions.Clear();

            if (filteredPlatforms.Count == 0)
            {
                NoAccountsLabel.IsVisible = true;
                NoAccountsLabel.Text = $"No platforms found matching '{searchText}'.";
                return;
            }
            
            NoAccountsLabel.IsVisible = false;

            bool isMobile = DeviceInfo.Idiom == DeviceIdiom.Phone;
            int columns = isMobile ? 1 : 2;

            for (int i = 0; i < columns; i++)
            {
                AccountsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            }

            int rowCount = (filteredPlatforms.Count + columns - 1) / columns;
            for (int i = 0; i < rowCount; i++)
            {
                AccountsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            for (int i = 0; i < filteredPlatforms.Count; i++)
            {
                var platform = filteredPlatforms[i];
                int row = i / columns;
                int col = i % columns;

                var platformFrame = CreatePlatformFrame(platform);
                AccountsGrid.Add(platformFrame, col, row);
            }
        }

    }
}