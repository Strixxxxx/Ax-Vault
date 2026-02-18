using System.Net.Http.Json;
using Frontend.Services;
using Frontend.Models;
using Frontend.Components.Toasts;

namespace Frontend.Components.Accounts.Pages
{
    public partial class MobileDeletePlatformPage : ContentPage
    {
        private readonly string _platformName;

        public MobileDeletePlatformPage(string platformName)
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
                    await Shell.Current.Navigation.PopAsync(); // Go back after successful deletion
                }
                else
                {
                    var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
                    ToastService.ShowToast(error?.Message ?? "Failed to delete platform.", ToastType.Error);
                    await ApiClient.SendErrorLog(new Exception(error?.Message ?? "Failed to delete platform."), "MobileDeletePlatformPage.OnDeleteClicked");
                }
            }
            catch (Exception ex)
            {
                await ApiClient.SendErrorLog(ex, "MobileDeletePlatformPage.OnDeleteClicked");
                ToastService.ShowToast($"An unexpected error occurred: {ex.Message}", ToastType.Error);
            }
        }

        private async void OnCancelClicked(object sender, EventArgs e)
        {
            await Shell.Current.Navigation.PopAsync(); // Go back on cancel
        }
    }
}
