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
                if (DashboardViewControl != null)
                {
                    // Access the parent MainLayoutComponent via the logical parent chain
                    // This assumes DashboardPage is hosted within MainLayout
                    var mainLayout = this.Parent as Frontend.Components.Layout.MainLayout;
                    if (mainLayout != null)
                    {
                        DashboardViewControl.Initialize(_username, mainLayout);
                    }
                    else
                    {
                        Console.WriteLine("Error: Could not find parent MainLayout component.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing dashboard: {ex.Message}");
                await DisplayAlertAsync("Error", "Failed to initialize dashboard. Please try logging out and back in.", "OK");
            }
        }
    }
} 