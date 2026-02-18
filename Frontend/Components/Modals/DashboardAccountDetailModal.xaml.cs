using Microsoft.Maui.Controls;
using System.Windows.Input;
using System.Threading.Tasks;
using Frontend.Services;
using System;
using Frontend.Models;
using System.Net.Http;
using System.Net.Http.Json;
using System.Collections.Generic;
using Microsoft.Maui.Dispatching; // Added for MainThread.BeginInvokeOnMainThread

namespace Frontend.Components.Modals
{
    public partial class DashboardAccountDetailModal : ContentView
    {
        private readonly AccountResponseModel _accountResponse;
        // private readonly bool _shouldRevealPassword; // Removed
        private readonly string _platformName; // Store platform name

        public DashboardAccountDetailModal(string platformName, AccountResponseModel accountResponse)
        {
            InitializeComponent();
            _platformName = platformName; // Assign passed platform name
            _accountResponse = accountResponse;
            // _shouldRevealPassword = revealPassword; // Removed
            PlatformName = platformName; // Set the public property
            Account = accountResponse;
            DisplayedPassword = "••••••••••••"; // Initially masked
            ShowPasswordCommand = new Command(async () => await TogglePasswordVisibility());
            BindingContext = this;

            Loaded += OnModalLoaded;
        }

        private void OnModalLoaded(object? sender, EventArgs e)
        {
            // Removed if (_shouldRevealPassword) await RevealPassword();
        }

        public AccountResponseModel Account { get; set; }
        public string PlatformName { get; set; } // Keep this public property for display

        private string? _displayedPassword;
        public string DisplayedPassword
        {
            get => _displayedPassword ?? "••••••••••••";
            set
            {
                if (_displayedPassword != value)
                {
                    _displayedPassword = value;
                    OnPropertyChanged(nameof(DisplayedPassword));
                }
            }
        }

        private string _showPasswordIcon = "&#xe8f4;"; // Visibility icon (eye closed)
        public string ShowPasswordIcon
        {
            get => _showPasswordIcon;
            set
            {
                if (_showPasswordIcon != value)
                {
                    _showPasswordIcon = value;
                    OnPropertyChanged(nameof(ShowPasswordIcon));
                }
            }
        }

        public ICommand ShowPasswordCommand { get; }

        public event EventHandler? CloseRequested;


        private async Task TogglePasswordVisibility()
        {
            if (DisplayedPassword == "••••••••••••")
            {
                await RevealPassword();
            }
            else
            {
                // Mask password
                DisplayedPassword = "••••••••••••";
                ShowPasswordIcon = "&#xe8f4;"; // Visibility icon (eye closed)
            }
        }

        private async Task RevealPassword()
        {
            string? vaultPassword = SessionService.Instance.VaultPassword;
            if (string.IsNullOrEmpty(vaultPassword))
            {
                await Shell.Current.DisplayAlertAsync("Error", "Vault password not available.", "OK");
                return;
            }

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, "api/accounts/decrypt-password");
                request.Content = JsonContent.Create(new
                {
                    Platform = _platformName, // Use the passed platform name for API call
                    Id = Account.Id,
                    VaultPassword = vaultPassword
                });

                var response = await ApiClient.Instance.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
                    if (result != null && result.TryGetValue("password", out string? decryptedPassword))
                    {
                        DisplayedPassword = decryptedPassword;
                        ShowPasswordIcon = "&#xe8f5;"; // Visibility_off icon (eye open)
                        
                        // Automatically re-mask after a delay
                        _ = Task.Delay(5000).ContinueWith(_ =>
                        {
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                // Only re-mask if still revealed
                                if (DisplayedPassword != "••••••••••••") 
                                {
                                    DisplayedPassword = "••••••••••••";
                                    ShowPasswordIcon = "&#xe8f4;";
                                }
                            });
                        });
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    await Shell.Current.DisplayAlertAsync("Error", $"Failed to decrypt password: {response.StatusCode} - {errorContent}", "OK");
                }
            }
            catch (Exception ex)
            {
                // Send error log to backend
                await ApiClient.SendErrorLog(ex, "DashboardAccountDetailModal.RevealPassword");
                await Shell.Current.DisplayAlertAsync("Error", "An unexpected error occurred while revealing the password. The issue has been reported.", "OK");
            }
        }

        private void OnCloseClicked(object sender, EventArgs e)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
