using System;
using System.Collections.Generic;

namespace Backend.Models.Backup
{
    /// <summary>
    /// The root model for the entire backup export payload.
    /// Contains versioning info and all platform data for a user.
    /// </summary>
    public class BackupExportModel
    {
        public int Version { get; set; } = 1;
        public List<PlatformBackupModel> Platforms { get; set; } = new();
    }

    /// <summary>
    /// Represents a single platform (e.g., "Google") and all its accounts.
    /// </summary>
    public class PlatformBackupModel
    {
        public string PlatformName { get; set; } = string.Empty;
        public List<AccountBackupModel> Accounts { get; set; } = new();
    }

    /// <summary>
    /// Represents a single account row. Passwords are stored in their
    /// already-encrypted form (encrypted by the vault key on the server).
    /// The frontend's BackupService will apply a second layer of AES-GCM
    /// encryption over the entire JSON, forming the Zero-Knowledge backup.
    /// </summary>
    public class AccountBackupModel
    {
        public string Username { get; set; } = string.Empty;  // Encrypted (vault-key encrypted)
        public string Password { get; set; } = string.Empty;  // Encrypted (vault-key encrypted)
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
