using System;
using System.Collections.ObjectModel; // Added
using System.Windows.Input;

namespace Frontend.Models
{
    public class AccountResponseModel
    {
        public long Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty; // This will store the encrypted password from DB
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? TimeZoneId { get; set; } // Added for timezone conversion
    }

    public class PlatformApiModel
    {
        public string Name { get; set; } = string.Empty;
        public int AccountCount { get; set; }
    }

    public class DashboardAccountItem : BindableObject
    {
        public long Id { get; set; }
        public string Username { get; set; } = string.Empty;
        private string? _password; // Stores the actual decrypted password for toggling
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public AccountResponseModel? OriginalAccount { get; set; } // Store the original account for decryption needs, made nullable
        public string? TimeZoneId { get; set; } // Added for timezone conversion

        private bool _isPasswordRevealed = false; // New property to track if password is shown
        public bool IsPasswordRevealed
        {
            get => _isPasswordRevealed;
            set
            {
                if (_isPasswordRevealed != value)
                {
                    _isPasswordRevealed = value;
                    OnPropertyChanged(nameof(IsPasswordRevealed));
                    OnPropertyChanged(nameof(DisplayedPassword)); // Notify DisplayedPassword changed
                    OnPropertyChanged(nameof(RevealPasswordIcon)); // Notify icon changed
                }
            }
        }

        public string DisplayedPassword
        {
            get => _isPasswordRevealed && !string.IsNullOrEmpty(_password) ? _password : "••••••••••••";
            set
            {
                if (_password != value) // Update the actual password field
                {
                    _password = value;
                    OnPropertyChanged(nameof(DisplayedPassword));
                }
            }
        }
        
        public string RevealPasswordIcon => IsPasswordRevealed ? "\uE8F5" : "\uE8F4"; // Visibility_off or visibility

        public ICommand? TogglePasswordVisibilityCommand { get; set; } // Command for the eye icon
        public PlatformItem? Platform { get; set; }
        // public ICommand? ViewAccountCommand { get; set; } // Removed as per new requirement
    }


    // Common UI model for platforms (used by Dashboard and AccountsView)
    public class PlatformItem : BindableObject
    {
        public string Icon { get; set; } = "📁";
        public string Name { get; set; } = string.Empty;
        public int AccountCount { get; set; }
        public string AccountCountDisplay => $"{AccountCount} accounts";
        public ICommand? ViewAccountsCommand { get; set; }
        
        public PlatformItem() { }
        
        public PlatformItem(string icon, string name, int accountCount)
        {
            Icon = icon;
            Name = name;
            AccountCount = accountCount;
        }
    }

    public class ErrorResponse
    {
        public string Message { get; set; } = string.Empty;
    }
}
