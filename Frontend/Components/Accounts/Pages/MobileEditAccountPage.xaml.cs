using Microsoft.Maui.Controls;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Frontend.Services;
using Frontend.Models;
using Frontend.Components.Toasts;
using System;

namespace Frontend.Components.Accounts.Pages
{
    public partial class MobileEditAccountPage : ContentPage
    {
        private readonly DashboardAccountItem _existingAccount;

        public MobileEditAccountPage(DashboardAccountItem existingAccount)
        {
            InitializeComponent();
            _existingAccount = existingAccount;
            TitleLabel.Text = $"Edit Account for {_existingAccount.Platform?.Name}";

            UsernameEntry.Text = _existingAccount.Username;
            DescriptionEditor.Text = _existingAccount.Description;
        }

        private async void OnConfirmClicked(object sender, EventArgs e)
        {
            string username = UsernameEntry.Text?.Trim() ?? "";
            string password = PasswordEntry.Text?.Trim() ?? "";
            string description = DescriptionEditor.Text?.Trim() ?? "";

            if (string.IsNullOrEmpty(username))
            {
                ToastService.ShowToast("Username is required.", ToastType.Error);
                return;
            }

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
                    Id = _existingAccount.Id,
                    Platform = _existingAccount.Platform?.Name, 
                    VaultPassword = vaultPassword, 
                    Username = username, 
                    Description = description,
                    Password = string.IsNullOrEmpty(password) ? null : password // Only send password if it's not empty
                };
                var response = await ApiClient.Instance.PutAsJsonAsync("api/accounts/edit", payload);
                
                if (response.IsSuccessStatusCode)
                {
                    ToastService.ShowToast("Account updated successfully!", ToastType.Success);
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
                await ApiClient.SendErrorLog(ex, "MobileEditAccountPage.OnConfirmClicked");
                ToastService.ShowToast($"An unexpected error occurred: {ex.Message}", ToastType.Error);
            }
        }

        private async void OnCancelClicked(object sender, EventArgs e)
        {
            await Shell.Current.Navigation.PopAsync(); // Go back without saving
        }
    }
}