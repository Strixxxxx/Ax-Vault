using Microsoft.Maui.Controls;
using System;
using System.Threading.Tasks;
using Frontend.Components.RouteGuard;

namespace Frontend.Components.Layout
{
    public partial class MainLayout : ContentView
    {
        private bool _isSidebarExpanded = true;
        private const double ExpandedSidebarWidth = 250;
        private const double CollapsedSidebarWidth = 60;
        private BoxView? _innerGlow;
        private BoxView? _outerGlow;
        private RouteGuardNavigator? _routeGuardNavigator;
        private string _username = string.Empty;
        private string? _token; // In-memory auth token
        
        // Events
        public event EventHandler? LogoutRequested;
        
        public MainLayout()
        {
            InitializeComponent();
            
            // Get references to the glow BoxViews after initialization
            _innerGlow = NeonDivider.Children[1] as BoxView;
            _outerGlow = NeonDivider.Children[2] as BoxView;
            
            // Start the glow animation
            StartGlowAnimation();
        }
        
        private void StartGlowAnimation()
        {
            // Create a subtle pulse animation for the neon effect
            var animation = new Animation();
            
            // Inner glow animation (if reference obtained)
            if (_innerGlow != null)
            {
                animation.Add(0, 0.5, new Animation(v => _innerGlow.Opacity = v, 0.3, 0.5));
                animation.Add(0.5, 1, new Animation(v => _innerGlow.Opacity = v, 0.5, 0.3));
            }
            
            // Outer glow animation (if reference obtained)
            if (_outerGlow != null)
            {
                animation.Add(0, 0.5, new Animation(v => _outerGlow.Opacity = v, 0.1, 0.3));
                animation.Add(0.5, 1, new Animation(v => _outerGlow.Opacity = v, 0.3, 0.1));
            }
            
            // Commit the animation with a duration of 2 seconds, repeating indefinitely
            animation.Commit(this, "GlowAnimation", 16, 2000, Easing.SinInOut, null, () => true);
        }
        
        public void SetUserSession(string username, string? token)
        {
            _username = username;
            _token = token;
            UsernameLabel.Text = username;
            
            // Initialize the route guard navigator with the username AND TOKEN
            if (this.Window?.Page?.Navigation != null)
            {
                _routeGuardNavigator = new RouteGuardNavigator(this.Window.Page.Navigation, _username, _token);
            }
        }
        
        private async void OnToggleSidebarClicked(object? sender, EventArgs e)
        {
            _isSidebarExpanded = !_isSidebarExpanded;
            
            // Determine target width
            var targetWidth = _isSidebarExpanded ? ExpandedSidebarWidth : CollapsedSidebarWidth;
            
            // Update UI based on sidebar state
            if (_isSidebarExpanded)
            {
                // Expand sidebar - show elements first then animate width
                ExpandedTopSection.IsVisible = true;
                CollapsedTopSection.IsVisible = false;
                CollapsedLogoView.IsVisible = false;
                ExpandedContent.IsVisible = true;
                LogoutButton.IsVisible = true;
                
                // Animate width
                await Sidebar.WidthRequestTo(targetWidth, 250, Easing.SpringOut);
            }
            else
            {
                // Collapse sidebar - animate width first then hide elements
                await Sidebar.WidthRequestTo(targetWidth, 250, Easing.SpringOut);
                
                // After animation completes, update UI
                ExpandedTopSection.IsVisible = false;
                CollapsedTopSection.IsVisible = true;
                CollapsedLogoView.IsVisible = true;
                ExpandedContent.IsVisible = false;
                LogoutButton.IsVisible = false;
            }
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
            
            // Highlight selected button
            DashboardButton.BackgroundColor = Color.FromArgb("#333333");
            AccountsButton.BackgroundColor = Colors.Transparent;
            BackupButton.BackgroundColor = Colors.Transparent;
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
            
            // Highlight selected button
            DashboardButton.BackgroundColor = Colors.Transparent;
            AccountsButton.BackgroundColor = Color.FromArgb("#333333");
            BackupButton.BackgroundColor = Colors.Transparent;
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
            DashboardButton.BackgroundColor = Colors.Transparent;
            AccountsButton.BackgroundColor = Colors.Transparent;
            BackupButton.BackgroundColor = Color.FromArgb("#333333");
        }
        
        private void OnLogoutClicked(object? sender, EventArgs e)
        {
            // Trigger logout event
            LogoutRequested?.Invoke(this, EventArgs.Empty);
        }
    }
    
    // Extension method for smooth animation
    public static class ViewExtensions
    {
        public static Task<bool> WidthRequestTo(this View view, double width, uint length = 250, Easing? easing = default)
        {
            var tcs = new TaskCompletionSource<bool>();
            
            var animation = new Animation(d => view.WidthRequest = d, view.WidthRequest, width);
            animation.Commit(view, "WidthAnimation", 16, length, easing, (d, b) => tcs.SetResult(true));
            
            return tcs.Task;
        }
    }
} 