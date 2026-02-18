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

namespace Frontend.Components.Accounts.Pages
{
    public partial class MobileAccountListPage : ContentPage
    {
        public PlatformItem Platform { get; set; }
        public ObservableCollection<DashboardAccountItem> Accounts { get; set; } = new ObservableCollection<DashboardAccountItem>();
        private List<DashboardAccountItem> _allAccounts = new List<DashboardAccountItem>();

        public bool HasAccounts => Accounts.Any();

        public MobileAccountListPage(PlatformItem platform)
        {
            InitializeComponent();
            Platform = platform;
            BindingContext = this;

            Loaded += OnPageLoaded;
        }

        private async void OnPageLoaded(object? sender, EventArgs e)
        {
            await LoadAccounts();
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            FilterAccounts(e.NewTextValue);
        }

        private void FilterAccounts(string searchText)
        {
            Accounts.Clear();
            if (string.IsNullOrWhiteSpace(searchText))
            {
                foreach (var account in _allAccounts)
                {
                    Accounts.Add(account);
                }
            }
            else
            {
                var filtered = _allAccounts.Where(a =>
                    a.Username.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    (a.Description?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false));
                foreach (var account in filtered)
                {
                    Accounts.Add(account);
                }
            }
            OnPropertyChanged(nameof(HasAccounts));
        }

        private async void OnAddAccountClicked(object sender, EventArgs e)
        {
            // Navigate to a new page for adding an account
            await Shell.Current.Navigation.PushAsync(new MobileAddAccountPage(Platform, async () => await LoadAccounts()));
        }

        private async void OnEditAccountTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is DashboardAccountItem account)
            {
                // Navigate to a new page for editing an account
                await Shell.Current.Navigation.PushAsync(new MobileEditAccountPage(account));
            }
        }

        private async void OnDeleteAccountTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is DashboardAccountItem account)
            {
                // Navigate to a new page for deleting an account
                await Shell.Current.Navigation.PushAsync(new MobileDeleteAccountPage(account));
            }
        }

        public async Task LoadAccounts()
        {
            _allAccounts.Clear();
            Accounts.Clear();

            string? vaultPassword = SessionService.Instance.VaultPassword;

            if (string.IsNullOrEmpty(vaultPassword))
            {
                ToastService.ShowToast("Vault password not available.", ToastType.Error);
                await Shell.Current.Navigation.PopAsync(); // Go back if no vault password
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
                                TogglePasswordVisibilityCommand = new Command<DashboardAccountItem>(async (acc) => await ExecuteTogglePasswordVisibilityCommand(acc))
                            };
                            _allAccounts.Add(dashboardAccountItem);
                        }
                        FilterAccounts(SearchEntry.Text); // Apply initial filter
                        OnPropertyChanged(nameof(HasAccounts));
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    ToastService.ShowToast($"Failed to load accounts: {response.StatusCode} - {errorContent}", ToastType.Error);
                    await Shell.Current.Navigation.PopAsync(); // Go back on error
                    OnPropertyChanged(nameof(HasAccounts));
                }
            }
            catch (Exception ex)
            {
                // Send error log to backend
                await ApiClient.SendErrorLog(ex, "MobileAccountListPage.LoadAccounts");
                ToastService.ShowToast($"An error occurred while loading accounts: {ex.Message}", ToastType.Error);
                await Shell.Current.Navigation.PopAsync(); // Go back on error
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
                    await ApiClient.SendErrorLog(ex, "MobileAccountListPage.ExecuteTogglePasswordVisibilityCommand");
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
    }
}