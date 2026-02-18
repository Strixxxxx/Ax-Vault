using System.Collections.ObjectModel;
using System.Net.Http.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Frontend.Services;
using Frontend.Components.RouteGuard;
using Frontend.Models;
using Frontend.Components.Toasts;

namespace Frontend.Components.Accounts.Modals
{
    public partial class AccountDetailModal : ContentView
    {
        private readonly string _platform;
        private string? _initialVaultPassword; // Added to store password passed via constructor
        public ObservableCollection<AccountResponseModel> Accounts { get; set; } = new ObservableCollection<AccountResponseModel>();
        private List<AccountResponseModel> _allAccounts = new List<AccountResponseModel>();

        public event EventHandler? CloseRequested;
        public event EventHandler<AccountFormEventArgs>? FormRequested;

        public class AccountFormEventArgs : EventArgs
        {
            public string Platform { get; set; } = string.Empty;
            public string VaultPassword { get; set; } = string.Empty;
            public AccountResponseModel? Account { get; set; }
        }

        public AccountDetailModal(string platform, string? vaultPassword = null) // Added vaultPassword parameter
        {
            InitializeComponent();
            _platform = platform;
            _initialVaultPassword = vaultPassword; // Store initial vault password
            PlatformTitle.Text = platform;
            AccountsCollection.ItemsSource = Accounts;

            Loaded += OnModalLoaded;
        }

        private async void OnModalLoaded(object? sender, EventArgs e)
        {
            await LoadAccounts();
        }

        public async Task LoadAccounts()
        {
            string? currentVaultPassword = SessionService.Instance.VaultPassword ?? _initialVaultPassword; // Prioritize SessionService
            
            if (string.IsNullOrEmpty(currentVaultPassword))
            {
                // This would be handled by RouteGuard. If it's empty here, something went wrong, or user cancelled.
                // For now, if currentVaultPassword is null here, it means the user cancelled the prompt.
                ToastService.ShowToast("Vault password required to load accounts.", ToastType.Error);
                CloseRequested?.Invoke(this, EventArgs.Empty);
                return;
            }

            try
            {
                var response = await ApiClient.Instance.PostAsJsonAsync("api/accounts/list", new { Platform = _platform, VaultPassword = currentVaultPassword });
                if (response.IsSuccessStatusCode)
                {
                    var accounts = await response.Content.ReadFromJsonAsync<List<AccountResponseModel>>();
                    if (accounts != null)
                    {
                        _allAccounts = accounts;
                        UpdateVisibleAccounts();
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    ToastService.ShowToast($"Failed to load accounts. Error: {errorContent}", ToastType.Error);
                    // Clear vault password if it failed, so user is prompted again
                    SessionService.Instance.VaultPassword = null;
                    CloseRequested?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                ToastService.ShowToast($"An unexpected error occurred: {ex.Message}", ToastType.Error);
                // Clear vault password on exception
                SessionService.Instance.VaultPassword = null;
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        private void UpdateVisibleAccounts()
        {
            string filter = SearchEntry.Text?.ToLower() ?? "";
            Accounts.Clear();
            foreach (var acc in _allAccounts.Where(a => a.Username.ToLower().Contains(filter)))
            {
                Accounts.Add(acc);
            }
            NoAccountsLabel.IsVisible = Accounts.Count == 0;
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e) => UpdateVisibleAccounts();

        private void OnAddAccountClicked(object sender, EventArgs e)
        {
            FormRequested?.Invoke(this, new AccountFormEventArgs { Platform = _platform, VaultPassword = SessionService.Instance.VaultPassword! });
        }

        private void OnEditAccountTapped(object sender, EventArgs e)
        {
             if (sender is Label label && label.GestureRecognizers[0] is TapGestureRecognizer tap && tap.CommandParameter is AccountResponseModel account)
             {
                FormRequested?.Invoke(this, new AccountFormEventArgs { Platform = _platform, VaultPassword = SessionService.Instance.VaultPassword!, Account = account });
             }
        }

        private async void OnDeleteAccountTapped(object sender, EventArgs e)
        {
             if (sender is Label label && label.GestureRecognizers[0] is TapGestureRecognizer tap && tap.CommandParameter is AccountResponseModel account)
             {
                 bool confirm = await App.Current!.Windows[0].Page!.DisplayAlertAsync("Delete Account", $"Are you sure you want to delete account '{account.Username}' for platform '{_platform}'?", "Delete", "Cancel");
                 if (!confirm) return;

                 try
                 {
                     var response = await ApiClient.Instance.PostAsJsonAsync("api/accounts/delete", new { Platform = _platform, Id = account.Id });
                     if (response.IsSuccessStatusCode)
                     {
                         ToastService.ShowToast($"Account '{account.Username}' deleted successfully!", ToastType.Success);
                         await LoadAccounts();
                     }
                     else
                     {
                         var errorContent = await response.Content.ReadAsStringAsync();
                         ToastService.ShowToast($"Failed to delete account: {errorContent}", ToastType.Error);
                     }
                 }
                 catch (Exception ex)
                 {
                     ToastService.ShowToast($"An unexpected error occurred: {ex.Message}", ToastType.Error);
                 }
             }
        }

        private void OnCloseClicked(object sender, EventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
