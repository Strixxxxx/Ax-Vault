using Microsoft.Maui.Controls;
using System;
using System.Threading.Tasks;
using Frontend.Components.RouteGuard;
using Frontend.Services;
using System.Threading;
using Frontend.Components.Modals;
using Microsoft.Maui.Devices; // Added for DeviceInfo

namespace Frontend.Components.Layout
{
    // Enum to represent the different navigation pages
    public enum PageType
    {
        Dashboard,
        Accounts,
        Backups
    }

    public partial class MainLayout : ContentView
    {
        private bool _isSidebarExpanded = true;
        private const double ExpandedSidebarWidth = 250;
        private const double CollapsedSidebarWidth = 60;
        private BoxView? _mobileOverlay; // Added for mobile overlay
        private RouteGuardNavigator? _routeGuardNavigator;
        private string _username = string.Empty;
        private string? _token; // In-memory auth token
        private PageType _currentPage; // Track the currently active page
        private Grid? _mobileNeonDivider; // Reference to the mobile-specific NeonDivider
        
        // Events
        public event EventHandler? LogoutRequested;

        // Public properties to expose UI components
        public Dashboard.DashboardView DashboardComponentInstance => DashboardComponent;
        public Accounts.AccountsView AccountsComponentInstance => AccountsComponent;
        public Backups.BackupsView BackupsComponentInstance => BackupsComponent;

        // Event args for requesting a new modal to be shown
        public class ModalRequestEventArgs : EventArgs
        {
            public ContentView? Modal { get; set; }
        }

        // Public method to show modals
        public void ShowModal(ContentView modalContent)
        {
            ModalPlaceholder.Content = modalContent;
            ModalContainer.IsVisible = true;
            ModalContainer.InputTransparent = false; // Allow interaction with modal
            
            if (modalContent is DashboardAccountListModal dalm)
            {
                dalm.CloseRequested += OnModalCloseRequested;
            }
            else if (modalContent is DashboardAccountDetailModal dadm)
            {
                dadm.CloseRequested += OnModalCloseRequested;
            }
        }

        private void OnModalCloseRequested(object? sender, EventArgs e)
        {
            HideModal();
        }

        // Public method to hide modals
        public void HideModal()
        {
            // Unsubscribe from any modal events before clearing
            if (ModalPlaceholder.Content is DashboardAccountListModal dalm)
            {
                dalm.CloseRequested -= OnModalCloseRequested;
            }
            else if (ModalPlaceholder.Content is DashboardAccountDetailModal dadm)
            {
                dadm.CloseRequested -= OnModalCloseRequested;
            }

            ModalPlaceholder.Content = null;
            ModalContainer.IsVisible = false;
            ModalContainer.InputTransparent = true; // Block interaction with underlying page
        }


        public MainLayout()
        {
            InitializeComponent();

            // Initialize and add the mobile overlay
            _mobileOverlay = new BoxView
            {
                BackgroundColor = Colors.Black,
                Opacity = 0,
                IsVisible = false,
                InputTransparent = true // Initially transparent to touch
            };
            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += OnMobileOverlayTapped;
            _mobileOverlay.GestureRecognizers.Add(tapGesture);

            MainGrid.Children.Add(_mobileOverlay);
            Grid.SetColumnSpan(_mobileOverlay, 3); // Span all columns initially
            _mobileOverlay.ZIndex = 100; // Below sidebar (200), above content (default 0)

            // Get reference to the mobile-specific NeonDivider
            _mobileNeonDivider = this.FindByName<Grid>("MobileNeonDivider");

            if (DeviceInfo.Idiom == DeviceIdiom.Phone)
            {
                // --- One-time setup for Mobile Overlay Mode ---
                // Place Sidebar and ContentArea in the same column (0) and span the content
                Grid.SetColumn(ContentArea, 0);
                Grid.SetColumnSpan(ContentArea, 3); // Let content take the full width
                Grid.SetColumn(Sidebar, 0);

                // Ensure the sidebar is on top
                Sidebar.ZIndex = 200;

                // Hide desktop divider
                NeonDivider.IsVisible = false;

                // Set initial state to collapsed for mobile
                _isSidebarExpanded = false;
                Sidebar.WidthRequest = CollapsedSidebarWidth; // Start with collapsed width

                // Apply collapsed visual state immediately for mobile
                ExpandedTopSection.IsVisible = false;
                CollapsedTopSection.IsVisible = true;
                ExpandedContent.IsVisible = false;
                CollapsedLogoView.IsVisible = true;
                LogoutButton.IsVisible = false;
            }
            else
            {
                // Set initial state for Desktop
                _isSidebarExpanded = true;
                
                // Apply expanded visual state immediately for desktop
                ExpandedTopSection.IsVisible = true;
                CollapsedTopSection.IsVisible = false;
                ExpandedContent.IsVisible = true;
                CollapsedLogoView.IsVisible = false;
                LogoutButton.IsVisible = true;
            }

            // Apply initial visual state without animation (this will handle overlay state)
            ApplySidebarVisualState(false);

            // Set initial page
            ShowDashboard();

            // Initialize ToastService
            ToastService.Initialize(ToastContainer);
        }

        private async void OnMobileOverlayTapped(object? sender, TappedEventArgs e)
        {
            if (_isSidebarExpanded)
            {
                _isSidebarExpanded = false;
                await ApplySidebarLayout(true);
            }
        }

        private async Task ApplySidebarLayout(bool animated)
        {
            uint animationLength = animated ? 250u : 0u;
            var targetWidth = _isSidebarExpanded ? ExpandedSidebarWidth : CollapsedSidebarWidth;

            if (DeviceInfo.Idiom == DeviceIdiom.Phone)
            {
                var targetOverlayOpacity = _isSidebarExpanded ? 0.5 : 0;

                if (animated)
                {
                    if (_mobileOverlay != null)
                    {
                        await Task.WhenAll(
                            Sidebar.WidthRequestTo(targetWidth, animationLength, Easing.CubicOut),
                            _mobileOverlay.FadeToAsync(targetOverlayOpacity, animationLength)
                        );
                    }
                }
                else
                {
                    Sidebar.WidthRequest = targetWidth;
                    if (_mobileOverlay != null)
                    {
                        _mobileOverlay.Opacity = targetOverlayOpacity;
                    }
                }
            }
            else // Desktop
            {
                if (animated)
                {
                    await Sidebar.WidthRequestTo(targetWidth, animationLength, Easing.CubicOut);
                }
                else
                {
                    Sidebar.WidthRequest = targetWidth;
                }
            }

            ApplySidebarVisualState(animated);
        }

        private void ApplySidebarVisualState(bool animated)
        {
            if (DeviceInfo.Idiom == DeviceIdiom.Phone)
            {
                if (_mobileOverlay != null)
                {
                    _mobileOverlay.IsVisible = _isSidebarExpanded;
                    _mobileOverlay.InputTransparent = !_isSidebarExpanded;
                }
                
                if (_mobileNeonDivider != null)
                {
                    _mobileNeonDivider.IsVisible = true; // Always visible on mobile
                }
            }

            // No counter-translation needed anymore
            CollapsedTopSection.TranslationX = 0;
            CollapsedLogoView.TranslationX = 0;

            // Common visual state updates for both platforms
            CollapsedTopSection.IsVisible = !_isSidebarExpanded;
            CollapsedLogoView.IsVisible = !_isSidebarExpanded;
            ExpandedTopSection.IsVisible = _isSidebarExpanded;
            ExpandedContent.IsVisible = _isSidebarExpanded;
            LogoutButton.IsVisible = _isSidebarExpanded;
        }
        
        public void SetUserSession(string username, string? token)
        {
            _username = username;
            _token = token;
            UsernameLabel.Text = username;
            
            // Ensure global ApiClient has the token
            ApiClient.SetAuthToken(token);

            // Initialize the route guard navigator with the username AND TOKEN
            if (this.Window?.Page?.Navigation != null)
            {
                _routeGuardNavigator = new RouteGuardNavigator(this.Window.Page.Navigation, _username, _token);
            }
        }
        
        private async void OnToggleSidebarClicked(object? sender, EventArgs e)
        {
            _isSidebarExpanded = !_isSidebarExpanded;
            await ApplySidebarLayout(true);
        }
        
        private async void OnDashboardClicked(object? sender, EventArgs e)
        {
            // First, show the route guard if needed
            if (_routeGuardNavigator != null && this.Window?.Page?.Navigation != null)
            {
                var taskCompletionSource = new TaskCompletionSource<bool>();

                var routeGuardPage = new RouteGuardPage(
                    _username,
                    "Dashboard",
                    () =>
                    {
                        // On success callback
                        taskCompletionSource.SetResult(true);
                    },
                    () =>
                    {
                        // On cancel callback
                        taskCompletionSource.SetResult(false);
                    },
                    _token // Pass token
                );

                await this.Window.Page.Navigation.PushModalAsync(routeGuardPage);

                // Wait for the user to complete the verification
                bool isAuthorized = await taskCompletionSource.Task;

                if (isAuthorized)
                {
                    // User is authorized, show the dashboard
                    ShowDashboard();
                }
                // If not authorized, do nothing
            }
            else
            {
                // No route guard navigator, just show the dashboard
                ShowDashboard();
            }
        }
        
        private void ShowDashboard()
        {
            // Show Dashboard, hide others
            DashboardComponent.IsVisible = true;
            AccountsComponent.IsVisible = false;
            BackupsComponent.IsVisible = false;

            // Refresh dashboard data
            DashboardComponent.Initialize(_username, this); // Pass username and MainLayout instance
            
            // Highlight selected button
            UpdateSidebarSelection(PageType.Dashboard);
        }
        
        private async void OnAccountsClicked(object? sender, EventArgs e)
        {
            // First, show the route guard if needed
            if (_routeGuardNavigator != null && this.Window?.Page?.Navigation != null)
            {
                var taskCompletionSource = new TaskCompletionSource<bool>();

                var routeGuardPage = new RouteGuardPage(
                    _username,
                    "Accounts",
                    () =>
                    {
                        // On success callback
                        taskCompletionSource.SetResult(true);
                    },
                    () =>
                    {
                        // On cancel callback
                        taskCompletionSource.SetResult(false);
                    },
                    _token // Pass token
                );

                await this.Window.Page.Navigation.PushModalAsync(routeGuardPage);

                // Wait for the user to complete the verification
                bool isAuthorized = await taskCompletionSource.Task;

                if (isAuthorized)
                {
                    // User is authorized, show the accounts
                    ShowAccounts();
                }
                // If not authorized, do nothing
            }
            else
            {
                // No route guard navigator, just show the accounts
                ShowAccounts();
            }
        }
        
        private void ShowAccounts()
        {
            // Show Accounts, hide others
            DashboardComponent.IsVisible = false;
            AccountsComponent.IsVisible = true;
            BackupsComponent.IsVisible = false;

            // Refresh platforms to ensure latest session data is used
            AccountsComponent.LoadPlatforms();
            
            // Highlight selected button
            UpdateSidebarSelection(PageType.Accounts);
        }
        
        private async void OnBackupClicked(object? sender, EventArgs e)
        {
            // First, show the route guard if needed
            if (_routeGuardNavigator != null && this.Window?.Page?.Navigation != null)
            {
                var taskCompletionSource = new TaskCompletionSource<bool>();
                
                var routeGuardPage = new RouteGuardPage(
                    _username,
                    "Backups",
                    () => 
                    {
                        // On success callback
                        taskCompletionSource.SetResult(true);
                    },
                    () => 
                    {
                        // On cancel callback
                        taskCompletionSource.SetResult(false);
                    },
                    _token // Pass token
                );

                await this.Window.Page.Navigation.PushModalAsync(routeGuardPage);

                // Wait for the user to complete the verification
                bool isAuthorized = await taskCompletionSource.Task;
                
                if (isAuthorized)
                {
                    // User is authorized, show the backups
                    ShowBackups();
                }
                // If not authorized, do nothing
            }
            else
            {
                // No route guard navigator, just show the backups
                ShowBackups();
            }
        }
        
        private void ShowBackups()
        {
            // Show Backup, hide others
            DashboardComponent.IsVisible = false;
            AccountsComponent.IsVisible = false;
            BackupsComponent.IsVisible = true;
            
            // Highlight selected button
            UpdateSidebarSelection(PageType.Backups);
        }
        
        private void OnLogoutClicked(object? sender, EventArgs e)
        {
            // Trigger logout event
            LogoutRequested?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateSidebarSelection(PageType currentPage)
        {
            // Reset all buttons to default state
            DashboardButton.IsEnabled = true;
            DashboardButton.TextColor = Colors.White;
            DashboardButton.BackgroundColor = Colors.Transparent;

            AccountsButton.IsEnabled = true;
            AccountsButton.TextColor = Colors.White;
            AccountsButton.BackgroundColor = Colors.Transparent;

            BackupButton.IsEnabled = true;
            BackupButton.TextColor = Colors.White;
            BackupButton.BackgroundColor = Colors.Transparent;

            // Set state for the current page's button
            switch (currentPage)
            {
                case PageType.Dashboard:
                    DashboardButton.IsEnabled = false;
                    DashboardButton.TextColor = Colors.Gray; // Grey out
                    DashboardButton.BackgroundColor = Color.FromArgb("#333333"); // Highlight background
                    break;
                case PageType.Accounts:
                    AccountsButton.IsEnabled = false;
                    AccountsButton.TextColor = Colors.Gray; // Grey out
                    AccountsButton.BackgroundColor = Color.FromArgb("#333333"); // Highlight background
                    break;
                case PageType.Backups:
                    BackupButton.IsEnabled = false;
                    BackupButton.TextColor = Colors.Gray; // Grey out
                    BackupButton.BackgroundColor = Color.FromArgb("#333333"); // Highlight background
                    break;
            }
            _currentPage = currentPage; // Update the private field
        }
    }
    
    // Extension method for smooth animation
    public static class ViewExtensions
    {
        public static Task<bool> WidthRequestTo(this VisualElement view, double width, uint length = 250, Easing? easing = null)
        {
            var tcs = new TaskCompletionSource<bool>();
            var animation = new Animation(v => view.WidthRequest = v, view.WidthRequest, width, easing);
            animation.Commit(view, "WidthAnimation", 16, length, finished: (v, c) => tcs.SetResult(c));
            return tcs.Task;
        }
    }
}