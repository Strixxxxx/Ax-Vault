using Microsoft.Maui.Controls;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Frontend.Services;
using Frontend.Models;
using Frontend.Components.Toasts;
using System;

namespace Frontend.Components.Accounts.Pages
{
    public partial class MobileAddAccountPage : ContentPage
    {
        private readonly PlatformItem _platform;
        private readonly Action? _onAccountAdded;

        public MobileAddAccountPage(PlatformItem platform, Action? onAccountAdded = null)
        {
            InitializeComponent();
            _platform = platform;
            _onAccountAdded = onAccountAdded;
            TitleLabel.Text = $"Add Account for {_platform.Name}";
        }

        private async void OnConfirmClicked(object sender, EventArgs e)
        {
            string username = UsernameEntry.Text?.Trim() ?? "";
            string password = PasswordEntry.Text?.Trim() ?? "";
            string description = DescriptionEditor.Text?.Trim() ?? "";

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ToastService.ShowToast("Username and Password are required.", ToastType.Error);
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
                    Platform = _platform.Name, 
                    VaultPassword = vaultPassword, 
                    Username = username, 
                    Password = password, 
                    Description = description 
                };
                var response = await ApiClient.Instance.PostAsJsonAsync("api/accounts/add", payload);
                
                if (response.IsSuccessStatusCode)
                {
                    ToastService.ShowToast("Account created successfully!", ToastType.Success);
                    _onAccountAdded?.Invoke();
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
                await ApiClient.SendErrorLog(ex, "MobileAddAccountPage.OnConfirmClicked");
                ToastService.ShowToast($"An unexpected error occurred: {ex.Message}", ToastType.Error);
            }
        }

        private async void OnCancelClicked(object sender, EventArgs e)
        {
            await Shell.Current.Navigation.PopAsync(); // Go back without saving
        }
    }
}