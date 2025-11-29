using System.ComponentModel.DataAnnotations;

namespace Backend.Models.Login;

public class LoginModel
{
    [Required]
    [StringLength(50, MinimumLength = 3)]
    public string Username { get; set; } = string.Empty;  // This can be either username or email
    
    [Required]
    [StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;
}

public class LoginResponseModel
{
    public string? Token { get; set; }
    public string? Username { get; set; }
    public string? Email { get; set; }
    public string? DatabaseName { get; set; }
    public string? Timezone { get; set; }
    public string Message { get; set; } = "Login successful";
} 