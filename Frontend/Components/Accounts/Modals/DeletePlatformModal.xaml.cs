using System.Net.Http.Json;
using Frontend.Services;
using Frontend.Models;
using Frontend.Components.Toasts;


namespace Frontend.Components.Accounts.Modals
{
    public partial class DeletePlatformModal : ContentView
    {
        private readonly string _platformName;
        public event EventHandler? PlatformsUpdated;
        public event EventHandler? CloseRequested;

        public DeletePlatformModal(string platformName)
        {
            InitializeComponent();
            _platformName = platformName;
            PlatformSpan.Text = platformName;
        }

        private async void OnDeleteClicked(object sender, EventArgs e)
        {
            string confirmation = ConfirmEntry.Text?.Trim() ?? "";
            if (confirmation != _platformName)
            {
                ToastService.ShowToast("Platform name does not match.", ToastType.Error);
                return;
            }

            try
            {
                var response = await ApiClient.Instance.PostAsJsonAsync("api/platforms/delete", new { Name = _platformName, ConfirmName = confirmation });
                if (response.IsSuccessStatusCode)
                {
                    ToastService.ShowToast($"Platform '{_platformName}' deleted successfully!", ToastType.Success);
                    PlatformsUpdated?.Invoke(this, EventArgs.Empty);
                    CloseRequested?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
                    ToastService.ShowToast(error?.Message ?? "Failed to delete platform.", ToastType.Error);
                }
            }
            catch (Exception ex)
            {
                ToastService.ShowToast($"An unexpected error occurred: {ex.Message}", ToastType.Error);
            }
        }

        private void OnCancelClicked(object sender, EventArgs e)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }


    }
}
