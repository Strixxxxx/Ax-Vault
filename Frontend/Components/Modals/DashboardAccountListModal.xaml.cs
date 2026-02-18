using Microsoft.Maui.Controls;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Frontend.Services;
using Frontend.Models;
using System.Collections.Generic;
using System.Windows.Input;
using System;
using System.Linq;
using static Frontend.Components.Layout.MainLayout;
using Frontend.Components.Toasts;
using Frontend.Components.Accounts.Pages;

namespace Frontend.Components.Modals
{
    public partial class DashboardAccountListModal : ContentView
    {
        public PlatformItem Platform { get; set; }
        public ObservableCollection<DashboardAccountItem> Accounts { get; set; } = new ObservableCollection<DashboardAccountItem>();

        public bool HasAccounts => Accounts.Any();

        public event EventHandler<ModalRequestEventArgs>? CloseRequested;

        public DashboardAccountListModal(PlatformItem platform)
        {
            InitializeComponent();
            Platform = platform;
            BindingContext = this;

            if (DeviceInfo.Idiom == DeviceIdiom.Phone)
            {
                AccountsCollectionView.ItemTemplate = (DataTemplate)this.Resources["MobileAccountItemTemplate"];
            }
            else
            {
                AccountsCollectionView.ItemTemplate = (DataTemplate)this.Resources["DesktopAccountItemTemplate"];
            }

            Loaded += OnModalLoaded;
        }

        private async void OnModalLoaded(object? sender, EventArgs e)
        {
            await LoadAccounts();
        }

        public async Task LoadAccounts()
        {
            Accounts.Clear();

            string? vaultPassword = SessionService.Instance.VaultPassword;

            if (string.IsNullOrEmpty(vaultPassword))
            {
                ToastService.ShowToast("Vault password not available.", ToastType.Error);
                CloseRequested?.Invoke(this, new ModalRequestEventArgs { Modal = null });
                return;
            }

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, "api/accounts/list");
                request.Content = JsonContent.Create(new { Platform = Platform.Name, VaultPassword = vaultPassword });

                var response = await ApiClient.Instance.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var accounts = await response.Content.ReadFromJsonAsync<List<AccountResponseModel>>();
                    if (accounts != null && accounts.Any())
                    {
                        foreach (var account in accounts)
                        {
                            var convertedCreatedAt = ConvertUtcToLocalTime(account.CreatedAt, account.TimeZoneId);

                            var dashboardAccountItem = new DashboardAccountItem
                            {
                                Id = account.Id,
                                Username = account.Username,
                                Description = account.Description,
                                CreatedAt = convertedCreatedAt,
                                OriginalAccount = account,
                                TimeZoneId = account.TimeZoneId,
                                TogglePasswordVisibilityCommand = new Command<DashboardAccountItem>(async (acc) => await ExecuteTogglePasswordVisibilityCommand(acc)),
                                Platform = this.Platform // Assign the platform from the modal's property
                            };
                            Accounts.Add(dashboardAccountItem);
                        }
                        OnPropertyChanged(nameof(HasAccounts));
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    ToastService.ShowToast($"Failed to load accounts: {response.StatusCode} - {errorContent}", ToastType.Error);
                    CloseRequested?.Invoke(this, new ModalRequestEventArgs { Modal = null });
                    OnPropertyChanged(nameof(HasAccounts));
                }
            }
            catch (Exception ex)
            {
                ToastService.ShowToast($"An error occurred while loading accounts: {ex.Message}", ToastType.Error);
                CloseRequested?.Invoke(this, new ModalRequestEventArgs { Modal = null });
                OnPropertyChanged(nameof(HasAccounts));
            }
        }

        private async Task ExecuteTogglePasswordVisibilityCommand(DashboardAccountItem account)
        {
            if (account.IsPasswordRevealed)
            {
                account.IsPasswordRevealed = false;
            }
            else
            {
                string? vaultPassword = SessionService.Instance.VaultPassword;
                if (string.IsNullOrEmpty(vaultPassword))
                {
                    ToastService.ShowToast("Vault password not available.", ToastType.Error);
                    return;
                }

                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, "api/accounts/decrypt-password");
                    request.Content = JsonContent.Create(new 
                    { 
                        Platform = Platform.Name,
                        Id = account.OriginalAccount?.Id, 
                        VaultPassword = vaultPassword 
                    });

                    var response = await ApiClient.Instance.SendAsync(request);
                    if (response.IsSuccessStatusCode)
                    {
                        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
                        if (result != null && result.TryGetValue("password", out string? decryptedPassword))
                        {
                            account.DisplayedPassword = decryptedPassword;
                            account.IsPasswordRevealed = true;

                            _ = Task.Delay(5000).ContinueWith(_ =>
                            {
                                MainThread.BeginInvokeOnMainThread(() =>
                                {
                                    if (account.IsPasswordRevealed)
                                    {
                                        account.IsPasswordRevealed = false;
                                    }
                                });
                            });
                        }
                    }
                    else
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        ToastService.ShowToast($"Failed to decrypt password: {response.StatusCode} - {errorContent}", ToastType.Error);
                    }
                }
                catch (Exception ex)
                {
                    // Send error log to backend
                    await ApiClient.SendErrorLog(ex, "DashboardAccountListModal.ExecuteTogglePasswordVisibilityCommand");
                    ToastService.ShowToast("An unexpected error occurred while revealing the password. The issue has been reported.", ToastType.Error);
                }
            }
        }

        private DateTime ConvertUtcToLocalTime(DateTime utcTime, string? timeZoneId)
        {
            if (string.IsNullOrEmpty(timeZoneId))
            {
                return utcTime.ToLocalTime();
            }

            try
            {
                TimeZoneInfo tzi = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                return TimeZoneInfo.ConvertTimeFromUtc(utcTime, tzi);
            }
            catch (TimeZoneNotFoundException)
            {
                return utcTime.ToLocalTime();
            }
            catch (Exception)
            {
                return utcTime.ToLocalTime();
            }
        }

        private void OnCloseClicked(object sender, EventArgs e)
        {
            CloseRequested?.Invoke(this, new ModalRequestEventArgs { Modal = null });
        }
    }
}