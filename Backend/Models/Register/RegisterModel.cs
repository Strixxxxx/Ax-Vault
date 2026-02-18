using System.ComponentModel.DataAnnotations;

namespace Backend.Models.Register
{
    public class RegisterModel
    {
        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;

        [Required]
        public string VaultPassword { get; set; } = string.Empty; // Renamed from VaultKey

        [Required]
        public string Timezone { get; set; } = string.Empty; // Still needed for conversions
    }
}