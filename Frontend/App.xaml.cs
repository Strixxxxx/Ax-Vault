using Microsoft.Maui.Platform;
using Microsoft.Maui.Storage;
using Frontend.Services;

namespace Frontend;

public partial class App : Application
{
	public App()
	{
		// Write to file for debugging since console output isn't visible
		var logPath = Path.Combine(FileSystem.AppDataDirectory, "debug.log");
		File.WriteAllText(logPath, $"[{DateTime.Now}] App Constructor Started\n");
		
		Console.WriteLine("=== App Constructor Started ===");
		
		// Load environment variables BEFORE InitializeComponent()
		try
		{
			File.AppendAllText(logPath, $"[{DateTime.Now}] Loading environment variables from EmbeddedResource...\n");
			
			// Load from Embedded Resource (Synchronous and Cross-Platform)
			var assembly = System.Reflection.Assembly.GetExecutingAssembly();
			// The Resource ID format is usually: Namespace.Folder.Filename
			// For Frontend folder Resources/Raw/app.env it should be: Frontend.Resources.Raw.app.env
			var resourceName = "Frontend.Resources.Raw.app.env";
			
			using var stream = assembly.GetManifestResourceStream(resourceName);
			
			if (stream == null)
			{
				// Debugging: List all available resources if not found
				var resources = string.Join(", ", assembly.GetManifestResourceNames());
				File.AppendAllText(logPath, $"[{DateTime.Now}] ERROR: Resource '{resourceName}' not found. Available resources: {resources}\n");
				throw new Exception($"Embedded resource '{resourceName}' not found.");
			}
			
			File.AppendAllText(logPath, $"[{DateTime.Now}] app.env stream opened successfully\n");
			
			DotNetEnv.Env.Load(stream);
			File.AppendAllText(logPath, $"[{DateTime.Now}] DotNetEnv.Env.Load completed\n");
			
			// Validate that critical environment variables are loaded
			var frontendSecret = AppSettings.FrontendSecret;
			File.AppendAllText(logPath, $"[{DateTime.Now}] FRONTEND_SECRET_KEY value: {(string.IsNullOrEmpty(frontendSecret) ? "NULL/EMPTY" : "SET")}\n");
			
			if (string.IsNullOrEmpty(frontendSecret))
			{
				File.AppendAllText(logPath, $"[{DateTime.Now}] ERROR: FRONTEND_SECRET_KEY is null or empty\n");
				throw new Exception("CRITICAL STARTUP ERROR: 'FRONTEND_SECRET_KEY' is not loaded from user configuration.");
			}
			
			File.AppendAllText(logPath, $"[{DateTime.Now}] ✅ Environment variables loaded successfully\n");
			Console.WriteLine("✅ Environment variables loaded successfully");
		}
		catch (Exception ex)
		{
			File.AppendAllText(logPath, $"[{DateTime.Now}] ❌ FATAL ERROR: {ex.Message}\n{ex.StackTrace}\n");
			Console.WriteLine($"❌ FATAL ERROR during environment variable loading: {ex.Message}");
			// Re-throw to prevent app from starting in invalid state
			throw;
		}
		
		// NOW initialize components (after environment variables are loaded)
		File.AppendAllText(logPath, $"[{DateTime.Now}] Calling InitializeComponent...\n");
		InitializeComponent();
		File.AppendAllText(logPath, $"[{DateTime.Now}] InitializeComponent completed\n");
		File.AppendAllText(logPath, $"[{DateTime.Now}] === App Constructor Completed ===\n");
		Console.WriteLine("=== App Constructor Completed ===");
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		Console.WriteLine("=== CreateWindow Started ===");
		
		try
		{
			var window = new Window(new AppShell());

			Console.WriteLine("AppShell created and set as window's page");
			
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

	protected override void OnStart()
	{
		base.OnStart();
		Console.WriteLine("=== App OnStart Started ===");
		
		// Environment variables are already loaded in the constructor
		// This method can be used for other startup tasks if needed
		Console.WriteLine("=== App OnStart Completed ===");
	}
}