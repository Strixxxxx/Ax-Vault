using System.ComponentModel.DataAnnotations;

namespace Backend.Models.RouteGuard
{
    public class RouteGuardRequest
    {
        [Required]
        public string VaultPassword { get; set; } = string.Empty; // Renamed from UniqueKey/VaultKey

        [Required]
        public string TargetModule { get; set; } = string.Empty;

        public string? UserIdentifier { get; set; } // Optional: Explicitly pass identifier to aid backend lookup
    }
}