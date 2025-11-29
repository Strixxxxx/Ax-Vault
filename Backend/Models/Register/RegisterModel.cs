using System.ComponentModel.DataAnnotations;

namespace Backend.Models.Register;

public class RegisterModel
{
    [Required]
    [StringLength(16, MinimumLength = 3)]
    public string Username { get; set; } = string.Empty;
    
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    [StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;
    
    [Required]
    [StringLength(100, MinimumLength = 6)]
    public string UniqueKey { get; set; } = string.Empty;

    [Required]
    public string Timezone { get; set; } = string.Empty;
} 