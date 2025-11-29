using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Frontend.Components.Dashboard;
using Frontend.Components.RouteGuard;
using System.Net.Http;
using System.Net.Http.Headers;
using Frontend.Services;

using Frontend.Components.Register;

namespace Frontend;

public partial class MainPage : ContentPage
{
	private string? _authToken;
	private string? _username;
	
	public MainPage()
	{
		InitializeComponent();
		
		// Subscribe to login view events
		LoginView.LoginSuccessful += OnLoginCompleted;
		LoginView.RegisterRequested += OnRegisterRequested;
        
        // Subscribe to register component events
        RegisterComponent.LoginRequested += OnLoginRequestedFromRegister;
        RegisterComponent.RegistrationCompleted += OnRegistrationCompleted;

        // Subscribe to main layout logout event
        MainLayoutComponent.LogoutRequested += OnLogoutRequested;
        
        // Check for existing token on startup
        CheckForExistingSession();
	}
	
	private void OnRegisterRequested(object? sender, EventArgs e)
	{
	    LoginView.IsVisible = false;
	    RegisterComponent.IsVisible = true;
	}
	
	private void OnLoginRequestedFromRegister(object? sender, EventArgs e)
	{
	    RegisterComponent.IsVisible = false;
	    RegisterComponent.ResetForm(); // Reset form state
	    LoginView.IsVisible = true;
	    LoginView.ResetForm();
	}

	private void OnRegistrationCompleted(object? sender, bool success)
	{
	    // This event is fired after registration is attempted.
	    // The `success` parameter is currently always false, and the `LoginRequested` event is also fired
	    // to transition back to the login screen. The `OnLoginRequestedFromRegister` handles the UI switch.
	    // We can simply ensure the forms are reset.
	    RegisterComponent.ResetForm();
	    LoginView.ResetForm();
	}

	
	private async void CheckForExistingSession()
	{
	    try
	    {
	        // Try to get stored token
	        var token = await SecureStorage.GetAsync("auth_token");
	        var username = await SecureStorage.GetAsync("username");
	        
	        if (!string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(username))
	        {
	            // Valid token exists, but show route guard first for security
	            _authToken = token;
	            _username = username;
	            ShowRouteGuardBeforeDashboard(username);
	        }
	    }
	    catch (Exception)
	    {
	        // Error accessing secure storage, just show login
	    }
	}

	private async void OnLoginCompleted(object? sender, bool successful)
	{
		if (successful)
		{
			try
			{
			    // Get the token from secure storage
			    _authToken = await SecureStorage.GetAsync("auth_token");
			    _username = await SecureStorage.GetAsync("username");
			    
			    // Show route guard first before showing dashboard
			    if (_authToken != null && _username != null)
			    {
			        ShowRouteGuardBeforeDashboard(_username);
			    }
			}
			catch (Exception ex)
			{
			    await DisplayAlert("Error", $"Failed to load user session: {ex.Message}", "OK");
			}
		}
	}
	
	private void ShowRouteGuardBeforeDashboard(string username)
	{
	    // Create a TaskCompletionSource to wait for route guard validation
	    var taskCompletionSource = new TaskCompletionSource<bool>();
	    
	    // Create the route guard page for the Dashboard module
	    var routeGuardPage = new RouteGuardPage(
	        username,
	        "Dashboard", // Target module is always Dashboard for login
	        () => 
	        {
	            // On success callback
	            taskCompletionSource.SetResult(true);
	        },
	        () => 
	        {
	            // On cancel callback - logout the user
	            taskCompletionSource.SetResult(false);
	            OnLogoutRequested(this, EventArgs.Empty);
	        }
	    );
	    
	    // Show the route guard as a modal
	    Navigation.PushModalAsync(routeGuardPage).ContinueWith(async (t) =>
	    {
	        // Wait for the user to complete verification
	        bool isAuthorized = await taskCompletionSource.Task;
	        
	        // On the UI thread
	        Dispatcher.Dispatch(() =>
	        {
	            if (isAuthorized)
	            {
	                // Only proceed to dashboard if authorization was successful
	                ShowDashboard(_authToken, username);
	            }
	        });
	    });
	}
	
	private void ShowDashboard(string token, string username)
	{
	    // Hide login view
	    LoginView.IsVisible = false;
        
	    // Set user info on main layout
	    MainLayoutComponent.SetUsername(username);
	    
	    // Show main layout
	    MainLayoutComponent.IsVisible = true;
	}

	private async void OnLogoutRequested(object? sender, EventArgs e)
	{
		string? token = await SecureStorage.GetAsync("auth_token");

		try
		{
			if (!string.IsNullOrEmpty(token))
			{
                var request = new HttpRequestMessage(HttpMethod.Post, "api/login/logout");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                await ApiClient.Instance.SendAsync(request);
			}
		}
		catch (Exception ex)
		{
			// Log the error but proceed with local logout anyway
			_ = DisplayAlert("Logout Error", "Could not contact the server, but you have been logged out locally.", "OK");
			System.Diagnostics.Debug.WriteLine($"Logout API call failed: {ex.Message}");
		}
		finally
		{
			// Clear auth token and username from memory
			_authToken = null;
			_username = null;
			
			// Clear secure storage by removing the keys
			SecureStorage.Remove("auth_token");
			SecureStorage.Remove("username");
			SecureStorage.Remove("database_name");
			
			// Return to login UI
			LoginView.ResetForm();
			LoginView.IsVisible = true;
			MainLayoutComponent.IsVisible = false;
		}
	}
}
