using System.Net.Http.Json;
using System.Threading.Tasks;
using Frontend.Services;
using Frontend.Models;
using Frontend.Components.Toasts;


namespace Frontend.Components.Accounts.Modals
{
    public partial class PlatformModal : ContentView
    {
        private readonly bool _isAddMode;
        private readonly string? _oldName;
        public event EventHandler? PlatformsUpdated;
        public event EventHandler? CloseRequested;

        public PlatformModal(bool isAddMode, string? oldName = null)
        {
            InitializeComponent();
            _isAddMode = isAddMode;
            _oldName = oldName;

            TitleLabel.Text = _isAddMode ? "Add Platform" : "Rename Platform";
            ConfirmButton.Text = _isAddMode ? "Add" : "Update";
            
            if (!_isAddMode && oldName != null)
            {
                NameEntry.Text = oldName;
            }
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
                bool success;
                if (_isAddMode)
                {
                    success = await AddPlatform(newName);
                }
                else
                {
                    success = await RenamePlatform(_oldName!, newName);
                }

                if (success)
                {
                    PlatformsUpdated?.Invoke(this, EventArgs.Empty);
                    CloseRequested?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                ToastService.ShowToast($"An unexpected error occurred: {ex.Message}", ToastType.Error);
            }
        }

        private async Task<bool> AddPlatform(string name)
        {
            var response = await ApiClient.Instance.PostAsJsonAsync("api/platforms/add", name);
            if (response.IsSuccessStatusCode)
            {
                ToastService.ShowToast($"Platform '{name}' added successfully!", ToastType.Success);
                return true;
            }
            
            string errorMsg = "Failed to add platform.";
            try
            {
                var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
                errorMsg = error?.Message ?? $"Error: {response.StatusCode}";
            }
            catch
            {
                // If it's not JSON, read as string
                var content = await response.Content.ReadAsStringAsync();
                errorMsg = !string.IsNullOrWhiteSpace(content) ? content : $"Server returned {response.StatusCode}";
            }

            ToastService.ShowToast(errorMsg, ToastType.Error);
            return false;
        }

        private async Task<bool> RenamePlatform(string oldName, string newName)
        {
            var response = await ApiClient.Instance.PutAsJsonAsync("api/platforms/edit", new { OldName = oldName, NewName = newName });
            if (response.IsSuccessStatusCode)
            {
                ToastService.ShowToast($"Platform '{oldName}' renamed to '{newName}' successfully!", ToastType.Success);
                return true;
            }
            
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
            return false;
        }

        private void OnCancelClicked(object sender, EventArgs e)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }


    }
}
