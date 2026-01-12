using Microsoft.Maui.Controls;
using System.Threading.Tasks;

namespace Frontend.Components.Dashboard
{
    public partial class DashboardPage : ContentPage
    {
        private readonly string _username;
        
        public DashboardPage(string username)
        {
            InitializeComponent();
            
            _username = username;
        }
        
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await InitializeDashboardAsync();
        }
        
        private async Task InitializeDashboardAsync()
        {
            try
            {
                // Get the database name from secure storage
                var databaseName = await SecureStorage.GetAsync("database_name") ?? string.Empty;
                
                // Initialize the DashboardView control
                if (DashboardViewControl != null)
                {
                    DashboardViewControl.Initialize(_username, databaseName);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing dashboard: {ex.Message}");
                await DisplayAlert("Error", "Failed to initialize dashboard. Please try logging out and back in.", "OK");
            }
        }
    }
} 