using System.ComponentModel.DataAnnotations;

namespace Backend.Models.RouteGuard
{
    public class RouteGuardRequest
    {
        [Required]
        public string Username { get; set; } = string.Empty;
        
        [Required]
        public string UniqueKey { get; set; } = string.Empty;
        
        [Required]
        public string TargetModule { get; set; } = string.Empty;
    }

    public class RouteGuardResponse
    {
        public bool IsAuthorized { get; set; }
        public string Message { get; set; } = string.Empty;
    }
} 