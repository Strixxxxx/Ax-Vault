using Microsoft.Extensions.Logging;
using System.Net.Http;
using Frontend.Components.RouteGuard;
using Frontend.Services;
using Microsoft.Maui.LifecycleEvents;
using System.IO;
// using Microsoft.UI;
// using Microsoft.UI.Windowing;
// using WinRT.Interop;

namespace Frontend;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();

        // Manually load settings from the embedded .env file
        LoadAppSettings().GetAwaiter().GetResult();

        // Add a startup validation check for the frontend secret
        if (string.IsNullOrEmpty(AppSettings.FrontendSecret))
        {
            throw new Exception("CRITICAL STARTUP ERROR: 'FRONTEND_SECRET_KEY' is not loaded. Please ensure a valid '.env' file with the key exists in the 'Frontend' project directory. The application cannot run without it.");
        }
		
		// Enable debug logging
		builder.Logging.AddDebug();
		
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
				fonts.AddFont("MaterialIcons-Regular.ttf", "MaterialIcons");
			})
			.ConfigureLifecycleEvents(events =>
			{
/*
#if WINDOWS
				events.AddWindows(windows => windows
					.OnWindowCreated(async (window) =>
					{
						Console.WriteLine("Window creation started...");
						
						// Get the HWND
						var handle = WindowNative.GetWindowHandle(window);
						Console.WriteLine("Got window handle");
						
						// Get AppWindow
						var windowId = Win32Interop.GetWindowIdFromWindow(handle);
						var appWindow = AppWindow.GetFromWindowId(windowId);
						
						if (appWindow != null)
						{
							Console.WriteLine("Got AppWindow instance");
							
							// Configure window
							var presenter = appWindow.Presenter as OverlappedPresenter;
							if (presenter != null)
							{
								presenter.IsResizable = true;
								presenter.IsMaximizable = true;
								presenter.IsMinimizable = true;
								presenter.SetBorderAndTitleBar(true, true);
								Console.WriteLine("Configured window properties");
							}
							
							// Set size and position
							appWindow.Resize(new Windows.Graphics.SizeInt32(1280, 720));
							
							var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
							if (displayArea != null)
							{
								var centerX = (displayArea.WorkArea.Width - 1280) / 2;
								var centerY = (displayArea.WorkArea.Height - 720) / 2;
								appWindow.Move(new Windows.Graphics.PointInt32(centerX, centerY));
								Console.WriteLine($"Positioned window at: {centerX}, {centerY}");
							}
							
							// Ensure window is visible and activated
							appWindow.Show();
							
							// Add a small delay to ensure proper initialization
							await Task.Delay(100);
							
							// Force activation and visibility
							if (presenter != null)
							{
								presenter.IsModal = false;
								presenter.Restore();
								
								// Try to force activation
								try
								{
									var hwnd = (IntPtr)handle;
									Microsoft.UI.WindowId newWindowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
									Microsoft.UI.Windowing.AppWindow newAppWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(newWindowId);
									newAppWindow.Show(true);
								}
								catch (Exception ex)
								{
									Console.WriteLine($"Error activating window: {ex.Message}");
								}
							}
							
							Console.WriteLine("Window setup completed");
						}
						else
						{
							Console.WriteLine("Failed to get AppWindow instance");
						}
					}));
#endif
*/
			});

		// Register services for dependency injection
		builder.Services.AddSingleton<RouteGuardService>();

		return builder.Build();
	}

    private static async Task LoadAppSettings()
    {
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync(".env");
            using var reader = new StreamReader(stream);
            
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line) || line.Trim().StartsWith("#"))
                {
                    continue;
                }

                var parts = line.Split('=', 2);
                if (parts.Length == 2 && parts[0].Trim() == "FRONTEND_SECRET_KEY")
                {
                    var value = parts[1].Trim();
                    if (value.StartsWith("\"") && value.EndsWith("\""))
                    {
                        value = value.Substring(1, value.Length - 2);
                    }
                    AppSettings.FrontendSecret = value;
                    Console.WriteLine("SUCCESS: Manually loaded FRONTEND_SECRET_KEY.");
                    return; // Exit after finding the key
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CRITICAL: Error loading/parsing .env file: {ex.Message}");
        }
    }
}
