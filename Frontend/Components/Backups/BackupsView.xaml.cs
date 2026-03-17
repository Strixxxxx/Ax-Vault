using Microsoft.Maui.Controls;
using Frontend.Services;
using Frontend.Components.Toasts;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Runtime.Versioning;

namespace Frontend.Components.Backups
{
    public partial class BackupsView : ContentView
    {
        private readonly BackupService _backupService;
        private string? _selectedFilePath;

        public BackupsView()
        {
            InitializeComponent();
            _backupService = new BackupService();
        }

        // ─────────────────── CREATE BACKUP ───────────────────

        [SupportedOSPlatform("ios13.0")]
        [SupportedOSPlatform("maccatalyst13.1")]
        [SupportedOSPlatform("android21.0")]
        private async void OnCreateBackupClicked(object sender, EventArgs e)
        {
            string vaultPassword = BackupPasswordEntry.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(vaultPassword))
            {
                ShowBackupStatus("Please enter your Vault Password.", isError: true);
                return;
            }

            SetLoading(true);
            ShowBackupStatus("Creating backup...", isError: false);

            try
            {
                byte[] zipBytes = await _backupService.CreateBackupZipAsync(vaultPassword);

                // Build a timestamped filename
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = $"AxVault_Backup_{timestamp}.zip";
                string savePath;

                if (DeviceInfo.Platform == DevicePlatform.WinUI)
                {
                    // For Windows: Create "Ax Vault Backup" in MyDocuments
                    string backupDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        "Ax Vault Backup");

                    if (!Directory.Exists(backupDir))
                    {
                        Directory.CreateDirectory(backupDir);
                    }

                    savePath = Path.Combine(backupDir, fileName);
                    await File.WriteAllBytesAsync(savePath, zipBytes);
                    
                    ToastService.ShowToast($"Backup saved to: {savePath}", ToastType.Success, 5000);
                    ShowBackupStatus($"✓ Backup saved: {savePath}", isError: false);
                }
                else
                {
                    // For Mobile (Android/iOS): Save to a temp file and use Share
                    savePath = Path.Combine(FileSystem.CacheDirectory, fileName);
                    await File.WriteAllBytesAsync(savePath, zipBytes);

                    await Share.Default.RequestAsync(new ShareFileRequest
                    {
                        Title = "Save Ax Vault Backup",
                        File = new ShareFile(savePath)
                    });

                    ShowBackupStatus("✓ Backup shared successfully.", isError: false);
                    ToastService.ShowToast("Backup shared successfully.", ToastType.Success);
                }

                BackupPasswordEntry.Text = string.Empty;
            }
            catch (Exception ex)
            {
                ShowBackupStatus($"✗ Backup failed: {ex.Message}", isError: true);
                ToastService.ShowToast("Backup failed.", ToastType.Error);
                await ApiClient.SendErrorLog(ex, "BackupsView.OnCreateBackupClicked");
            }
            finally
            {
                SetLoading(false);
            }
        }

        // ─────────────────── BROWSE FILE ───────────────────

        private async void OnBrowseFileClicked(object sender, EventArgs e)
        {
            try
            {
                var options = new PickOptions
                {
                    PickerTitle = "Select Ax Vault Backup File",
                    FileTypes = new FilePickerFileType(new System.Collections.Generic.Dictionary<DevicePlatform, System.Collections.Generic.IEnumerable<string>>
                    {
                        { DevicePlatform.WinUI, new[] { ".zip" } },
                        { DevicePlatform.Android, new[] { "application/zip" } },
                        { DevicePlatform.iOS, new[] { "public.zip-archive" } },
                        { DevicePlatform.MacCatalyst, new[] { "public.zip-archive" } }
                    })
                };

                var result = await FilePicker.Default.PickAsync(options);
                if (result != null)
                {
                    _selectedFilePath = result.FullPath;
                    SelectedFileLabel.Text = result.FileName;
                    SelectedFileLabel.TextColor = Microsoft.Maui.Graphics.Color.FromArgb("#FFFFFF");
                }
            }
            catch (Exception ex)
            {
                ShowRestoreStatus($"✗ Could not open file picker: {ex.Message}", isError: true);
            }
        }

        // ─────────────────── RESTORE BACKUP ───────────────────

        [SupportedOSPlatform("ios13.0")]
        [SupportedOSPlatform("maccatalyst13.1")]
        [SupportedOSPlatform("android21.0")]
        private async void OnRestoreBackupClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedFilePath))
            {
                ShowRestoreStatus("Please select a backup file first.", isError: true);
                return;
            }

            string vaultPassword = RestorePasswordEntry.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(vaultPassword))
            {
                ShowRestoreStatus("Please enter your Vault Password.", isError: true);
                return;
            }

            bool confirmed = await Application.Current!.Windows[0].Page!.DisplayAlertAsync(
                "Confirm Restore",
                "Restoring will add all accounts from the backup to your vault. Existing accounts will not be deleted. Are you sure?",
                "Yes, Restore",
                "Cancel");

            if (!confirmed) return;

            SetLoading(true);
            ShowRestoreStatus("Restoring backup...", isError: false);

            try
            {
                using var fileStream = File.OpenRead(_selectedFilePath);
                string jsonResult = await _backupService.RestoreBackupAsync(vaultPassword, fileStream);

                // Parse the JSON result to show a clean message
                string cleanMessage = "Restore complete.";
                try 
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(jsonResult);
                    if (doc.RootElement.TryGetProperty("message", out var msgProp))
                    {
                        cleanMessage = msgProp.GetString() ?? cleanMessage;
                    }
                }
                catch { /* fallback to default */ }

                RestorePasswordEntry.Text = string.Empty;
                _selectedFilePath = null;
                SelectedFileLabel.Text = "No file selected";
                SelectedFileLabel.TextColor = Microsoft.Maui.Graphics.Color.FromArgb("#555577");

                ShowRestoreStatus($"✓ {cleanMessage}", isError: false);
                ToastService.ShowToast(cleanMessage, ToastType.Success);
            }
            catch (Exception ex)
            {
                string friendlyMsg = ex.Message.Contains("auth tag") || ex.Message.Contains("GCM")
                    ? "✗ Incorrect Vault Password or corrupted backup file."
                    : $"✗ Restore failed: {ex.Message}";

                ShowRestoreStatus(friendlyMsg, isError: true);
                ToastService.ShowToast(friendlyMsg.Replace("✗ ", ""), ToastType.Error);
                await ApiClient.SendErrorLog(ex, "BackupsView.RestoreBackup");
            }
            finally
            {
                SetLoading(false);
            }
        }

        // ─────────────────── HELPERS ───────────────────

        private void OnTogglePassword(object sender, EventArgs e)
        {
            if (sender is Label label)
            {
                // Find the Entry in the same parent (Grid)
                if (label.Parent is Grid grid)
                {
                    foreach (var child in grid.Children)
                    {
                        if (child is Entry entry)
                        {
                            entry.IsPassword = !entry.IsPassword;
                            // Update icon: \ue8f4 (eye), \ue8f5 (eye-off)
                            label.Text = entry.IsPassword ? "\ue8f4" : "\ue8f5";
                            break;
                        }
                    }
                }
            }
        }

        private void ShowBackupStatus(string message, bool isError)
        {
            BackupStatusLabel.Text = message;
            BackupStatusLabel.TextColor = isError
                ? Microsoft.Maui.Graphics.Color.FromArgb("#ff4466")
                : Microsoft.Maui.Graphics.Color.FromArgb("#00e5ff");
            BackupStatusLabel.IsVisible = true;
        }

        private void ShowRestoreStatus(string message, bool isError)
        {
            RestoreStatusLabel.Text = message;
            RestoreStatusLabel.TextColor = isError
                ? Microsoft.Maui.Graphics.Color.FromArgb("#ff4466")
                : Microsoft.Maui.Graphics.Color.FromArgb("#00e5ff");
            RestoreStatusLabel.IsVisible = true;
        }

        private void SetLoading(bool isLoading)
        {
            LoadingIndicator.IsRunning = isLoading;
            LoadingIndicator.IsVisible = isLoading;
            CreateBackupButton.IsEnabled = !isLoading;
            RestoreBackupButton.IsEnabled = !isLoading;
        }
    }
}