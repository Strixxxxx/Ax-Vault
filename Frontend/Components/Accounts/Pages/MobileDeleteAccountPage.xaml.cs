using Microsoft.Maui.Controls;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Frontend.Services;
using Frontend.Models;
using Frontend.Components.Toasts;
using System;

namespace Frontend.Components.Accounts.Pages
{
    public partial class MobileDeleteAccountPage : ContentPage
    {
        private readonly DashboardAccountItem _accountToDelete;

        public MobileDeleteAccountPage(DashboardAccountItem accountToDelete)
        {
            InitializeComponent();
            _accountToDelete = accountToDelete;
            ConfirmationLabel.Text = $"Are you sure you want to delete the account '{_accountToDelete.Username}' for '{_accountToDelete.Platform?.Name}'?";
        }

        private async void OnDeleteClicked(object sender, EventArgs e)
        {
            try
            {
                string? vaultPassword = SessionService.Instance.VaultPassword;
                if (string.IsNullOrEmpty(vaultPassword))
                {
                    ToastService.ShowToast("Vault password not available.", ToastType.Error);
                    return;
                }

                var payload = new 
                { 
                    Id = _accountToDelete.Id,
                    Platform = _accountToDelete.Platform?.Name, 
                    VaultPassword = vaultPassword 
                };
                var response = await ApiClient.Instance.PostAsJsonAsync("api/accounts/delete", payload);
                
                if (response.IsSuccessStatusCode)
                {
                    ToastService.ShowToast("Account deleted successfully!", ToastType.Success);
                    await Shell.Current.Navigation.PopAsync(); // Go back to the account list
                }
                else
                {
                    var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
                    ToastService.ShowToast(error?.Message ?? $"Error: {response.StatusCode}", ToastType.Error);
                }
            }
            catch (Exception ex)
            {
                await ApiClient.SendErrorLog(ex, "MobileDeleteAccountPage.OnDeleteClicked");
                ToastService.ShowToast($"An unexpected error occurred: {ex.Message}", ToastType.Error);
            }
        }

        private async void OnCancelClicked(object sender, EventArgs e)
        {
            await Shell.Current.Navigation.PopAsync(); // Go back without deleting
        }
    }
}