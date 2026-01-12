using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace Frontend.Components.RouteGuard
{
    public class RouteGuardNavigator
    {
        private readonly INavigation _navigation;
        private readonly string _username;
        private readonly string? _token;
        
        // List of modules that require route guard protection
        private static readonly string[] ProtectedModules = new[]
        {
            "Dashboard",
            "Accounts",
            "Backups"
        };
        
        public RouteGuardNavigator(INavigation navigation, string? username, string? token = null)
        {
            _navigation = navigation;
            _username = username ?? string.Empty;
            _token = token;
        }
        
        /// <summary>
        /// Navigates to a page with route guard protection if needed
        /// </summary>
        /// <param name="targetPage">The page to navigate to</param>
        /// <param name="moduleName">The name of the module being accessed</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task NavigateToAsync(Page targetPage, string moduleName)
        {
            // Check if this module requires route guard protection
            if (IsProtectedModule(moduleName))
            {
                // Show route guard verification page
                var taskCompletionSource = new TaskCompletionSource<bool>();
                
                var routeGuardPage = new RouteGuardPage(
                    _username,
                    moduleName,
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
                    _token // Pass the in-memory token
                );
                
                await _navigation.PushModalAsync(routeGuardPage);
                
                // Wait for the user to complete the verification
                bool isAuthorized = await taskCompletionSource.Task;
                
                if (isAuthorized)
                {
                    // User is authorized, navigate to the target page
                    await _navigation.PushAsync(targetPage);
                }
                // If not authorized, we do nothing (modal was already closed by the RouteGuardPage)
            }
            else
            {
                // Module doesn't require route guard, navigate directly
                await _navigation.PushAsync(targetPage);
            }
        }
        
        /// <summary>
        /// Checks if a module requires route guard protection
        /// </summary>
        /// <param name="moduleName">The name of the module</param>
        /// <returns>True if the module requires protection, false otherwise</returns>
        private bool IsProtectedModule(string moduleName)
        {
            return Array.Exists(ProtectedModules, module => 
                string.Equals(module, moduleName, StringComparison.OrdinalIgnoreCase));
        }
    }
} 