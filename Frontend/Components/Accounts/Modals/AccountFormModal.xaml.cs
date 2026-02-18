using System.Net.Http.Json;
using System.Threading.Tasks;
using Frontend.Services;
using Frontend.Models;
using Frontend.Components.Toasts; // Added

namespace Frontend.Components.Accounts.Modals
{
    public partial class AccountFormModal : ContentView
    {
        private readonly string _platform;
        private readonly string _vaultPassword;
        private readonly AccountResponseModel? _existingAccount;
        private readonly bool _isAddMode;
        public event EventHandler? AccountUpdated;
        public event EventHandler? CloseRequested;

        public AccountFormModal(string platform, string vaultPassword, AccountResponseModel? existingAccount = null)
        {
            InitializeComponent();
            _platform = platform;
            _vaultPassword = vaultPassword;
            _existingAccount = existingAccount;
            _isAddMode = existingAccount == null;

            TitleLabel.Text = _isAddMode ? "Add Account" : "Edit Account";
            
            if (!_isAddMode && _existingAccount != null)
            {
                UsernameEntry.Text = _existingAccount.Username;
                DescriptionEditor.Text = _existingAccount.Description;
                PasswordEntry.Placeholder = "Enter new password if changing...";
            }
        }

        private async void OnConfirmClicked(object sender, EventArgs e)
        {
            string username = UsernameEntry.Text?.Trim() ?? "";
            string password = PasswordEntry.Text?.Trim() ?? "";
            string description = DescriptionEditor.Text?.Trim() ?? "";

            if (string.IsNullOrEmpty(username) || (_isAddMode && string.IsNullOrEmpty(password)))
            {
                ToastService.ShowToast("Username and Password are required.", ToastType.Error);
                return;
            }

            try
            {
                bool success;
                string successMessage = "";
                string errorMessage = "Operation failed. Check your inputs.";

                if (_isAddMode)
                {
                    var payload = new 
                    { 
                        Platform = _platform, 
                        VaultPassword = _vaultPassword, 
                        Username = username, 
                        Password = password, 
                        Description = description 
                    };
                    var response = await ApiClient.Instance.PostAsJsonAsync("api/accounts/add", payload);
                    success = response.IsSuccessStatusCode;
                    if (success) successMessage = "Account created successfully!";
                    else
                    {
                        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
                        errorMessage = error?.Message ?? $"Error: {response.StatusCode}";
                    }
                }
                else
                {
                    var payload = new 
                    { 
                        Id = _existingAccount!.Id,
                        Platform = _platform, 
                        VaultPassword = _vaultPassword, 
                        Username = username, 
                        Password = string.IsNullOrEmpty(password) ? "" : password, 
                        Description = description 
                    };
                    var response = await ApiClient.Instance.PutAsJsonAsync("api/accounts/edit", payload);
                    success = response.IsSuccessStatusCode;
                    if (success) successMessage = "Account updated successfully!";
                    else
                    {
                        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
                        errorMessage = error?.Message ?? $"Error: {response.StatusCode}";
                    }
                }

                if (success)
                {
                    ToastService.ShowToast(successMessage, ToastType.Success);
                    AccountUpdated?.Invoke(this, EventArgs.Empty);
                    CloseRequested?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    ToastService.ShowToast(errorMessage, ToastType.Error);
                }
            }
            catch (Exception ex)
            {
                ToastService.ShowToast($"An unexpected error occurred: {ex.Message}", ToastType.Error);
            }
        }

        private void OnCancelClicked(object sender, EventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);

    }
}
