using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Frontend.Components.Dashboard;
using Frontend.Components.RouteGuard;
using System.Net.Http;
using System.Net.Http.Headers;
using Frontend.Services;

using Frontend.Components.Register;
using Frontend.Components.Login; // Add this namespace

namespace Frontend;

public partial class MainPage : ContentPage
{
	private bool _hasCheckedSession = false; // Flag to prevent infinite loop on OnAppearing
	
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
	}

	   protected override async void OnAppearing()
	   {
	       base.OnAppearing();
	       try
	       {
	           // Prevent infinite loop: If we already checked session, or we are already logged in (MainLayout visible), skip.
	           if (_hasCheckedSession || MainLayoutComponent.IsVisible)
	               return;

	           await CheckForExistingSession();
	       }
	       catch (Exception ex)
	       {
	           // Show an alert if session check fails to prevent app crash on startup
	           await DisplayAlertAsync("Startup Error", $"An error occurred while checking for an existing session: {ex.Message}", "OK");
	       }
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

	
	private async Task CheckForExistingSession()
	{
	    _hasCheckedSession = true; // Mark as checked so we don't loop in OnAppearing
	    try
	    {
	        // Try to get stored token
	        var token = await SecureStorage.GetAsync("auth_token");
	        var username = await SecureStorage.GetAsync("username");
	        
	        if (!string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(username))
	        {
	            // Add a delay to allow startup animations to be visible
	            await Task.Delay(2500); // 2.5-second delay

	            // Update session service
	            SessionService.Instance.AuthToken = token;
	            SessionService.Instance.Username = username;
	            SessionService.Instance.UserIdentifier = username; // Assume identifier is the stored username

	            // Set global auth token
	            ApiClient.SetAuthToken(token);

	            // Valid token exists, but show route guard first for security
	            ShowRouteGuardBeforeDashboard(username);
	        }
	    }
	    catch (Exception)
	    {
	        // Error accessing secure storage, just show login
	    }
	}

	private async void OnLoginCompleted(object? sender, Login.LoginSuccessEventArgs e)
	{
		if (e.Success)
		{
			try
			{
			    // Use the values from the event args (whether stored or not)
			    SessionService.Instance.AuthToken = e.Token;
			    SessionService.Instance.Username = e.Username;
			    SessionService.Instance.UserIdentifier = e.Username;
			    
			    // Set global auth token
			    ApiClient.SetAuthToken(e.Token);

			    // CRITICAL FIX: Mark session as checked so we don't double-prompt inside OnAppearing
			    _hasCheckedSession = true;

			    // Show route guard first before showing dashboard
			    if (e.Token != null && e.Username != null)
			    {
			        ShowRouteGuardBeforeDashboard(e.Username);
			    }
			}
			catch (Exception ex)
			{
			    await DisplayAlertAsync("Error", $"Failed to load user session: {ex.Message}", "OK");
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
	            // On cancel callback - clear vault password and hide main layout
	            taskCompletionSource.SetResult(false);
	            SessionService.Instance.VaultPassword = null; // Clear vault password
	            MainLayoutComponent.IsVisible = false; // Hide main layout
	            LoginView.IsVisible = true; // Show login again
	        },
	        SessionService.Instance.AuthToken // Pass the in-memory token
	    );
	    
	    // Show the route guard as a modal
	    Navigation.PushModalAsync(routeGuardPage).ContinueWith(async (t) =>
	    {
	        // Wait for the user to complete verification
	        bool isAuthorized = await taskCompletionSource.Task;
	        
	        // On the UI thread
	        Dispatcher.Dispatch(() =>
	        {
	            if (isAuthorized && SessionService.Instance.AuthToken != null)
	            {
	                // Only proceed to dashboard if authorization was successful
	                ShowDashboard(SessionService.Instance.AuthToken, username);
	            }
	        });
	    });
	}
	
	private void ShowDashboard(string token, string username)
	{
	    // Hide login view
	    LoginView.IsVisible = false;
	       
	    // Set user info on main layout
	    MainLayoutComponent.SetUserSession(username, token);
	    
	    // Show main layout
	    MainLayoutComponent.IsVisible = true;
	    
	    // Initialize dashboard after MainLayoutComponent is visible and user session is set
	    MainLayoutComponent.DashboardComponentInstance.Initialize(username, MainLayoutComponent);
	}

	private async void OnLogoutRequested(object? sender, EventArgs e)
	{
		string? token = SessionService.Instance.AuthToken;

		try
		{
			if (!string.IsNullOrEmpty(token))
			{
                var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/logout");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                await ApiClient.Instance.SendAsync(request);
			}
		}
		catch (Exception ex)
		{
			// Log the error but proceed with local logout anyway
			await DisplayAlertAsync("Logout Error", "Could not contact the server, but you have been logged out locally.", "OK");
			System.Diagnostics.Debug.WriteLine($"Logout API call failed: {ex.Message}");
		}
		finally
		{
			// Clear session service
			SessionService.Instance.Clear();
			
			// Clear secure storage
			SecureStorage.Remove("auth_token");
			SecureStorage.Remove("username");
			
			// Clear global auth token
			ApiClient.SetAuthToken(null);

			// Return to login UI
			LoginView.ResetForm();
			LoginView.IsVisible = true;
			MainLayoutComponent.IsVisible = false;
			
			// Reset session check flag so user can login again
			_hasCheckedSession = false; 
		}
	}
}
