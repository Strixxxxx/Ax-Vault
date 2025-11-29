using Microsoft.Maui.Platform;

namespace Frontend;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
		Console.WriteLine("=== App Constructor Started ===");

		// Create the main page first
		var appShell = new AppShell();
		Console.WriteLine("AppShell created");

		// Set the main page
		MainPage = appShell;
		Console.WriteLine("MainPage set to AppShell");
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		Console.WriteLine("=== CreateWindow Started ===");
		
		try
		{
			var window = base.CreateWindow(activationState);
			
			// Configure window properties
			window.Title = "Ax Vault";
			window.Width = 1280;
			window.Height = 720;
			window.X = -1; // Let the system position the window
			window.Y = -1;
			
			window.Created += (s, e) => 
			{
				Console.WriteLine("Window Created event fired");
				// Force window to be visible
				MainThread.BeginInvokeOnMainThread(() => {
					try 
					{
						window.MinimumWidth = 800;
						window.MinimumHeight = 600;
						window.MaximumWidth = 1920;
						window.MaximumHeight = 1080;
					}
					catch (Exception ex)
					{
						Console.WriteLine($"Error configuring window: {ex.Message}");
					}
				});
			};
			
			window.Activated += (s, e) => 
			{
				Console.WriteLine("Window Activated event fired");
			};
			
			window.Destroying += (s, e) => 
			{
				Console.WriteLine("Window Destroying event fired");
			};
			
			Console.WriteLine("Window configured successfully");
			Console.WriteLine("=== CreateWindow Completed ===");
			
			return window;
		}
		catch (Exception ex)
		{
			Console.WriteLine($"ERROR in CreateWindow: {ex.Message}");
			Console.WriteLine($"Stack trace: {ex.StackTrace}");
			throw;
		}
	}
}