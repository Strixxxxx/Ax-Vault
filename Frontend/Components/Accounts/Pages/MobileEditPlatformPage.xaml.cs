using System.Net.Http.Json;
using System.Threading.Tasks;
using Frontend.Services;
using Frontend.Models;
using Frontend.Components.Toasts;
using Microsoft.Maui.Controls;
using System;

namespace Frontend.Components.Accounts.Pages
{
    public partial class MobileEditPlatformPage : ContentPage
    {
        private readonly string _oldName;

        public MobileEditPlatformPage(string oldName)
        {
            InitializeComponent();
            _oldName = oldName;
            NameEntry.Text = oldName;
        }

        private async void OnConfirmClicked(object sender, EventArgs e)
        {
            string newName = NameEntry.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(newName))
            {
                ToastService.ShowToast("Name cannot be empty.", ToastType.Error);
                return;
            }

            try
            {
                var response = await ApiClient.Instance.PutAsJsonAsync("api/platforms/edit", new { OldName = _oldName, NewName = newName });
                if (response.IsSuccessStatusCode)
                {
                    ToastService.ShowToast($"Platform '{_oldName}' renamed to '{newName}' successfully!", ToastType.Success);
                    await Shell.Current.Navigation.PopAsync(); // Go back to the previous page
                }
                else
                {
                    string errorMsg = "Failed to rename platform.";
                    try
                    {
                        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
                        errorMsg = error?.Message ?? $"Error: {response.StatusCode}";
                    }
                    catch
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        errorMsg = !string.IsNullOrWhiteSpace(content) ? content : $"Server returned {response.StatusCode}";
                    }
                    ToastService.ShowToast(errorMsg, ToastType.Error);
                }
            }
            catch (Exception ex)
            {
                await ApiClient.SendErrorLog(ex, "MobileEditPlatformPage.OnConfirmClicked");
                ToastService.ShowToast($"An unexpected error occurred: {ex.Message}", ToastType.Error);
            }
        }

        private async void OnCancelClicked(object sender, EventArgs e)
        {
            await Shell.Current.Navigation.PopAsync(); // Go back without saving
        }
    }
}