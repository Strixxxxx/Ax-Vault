using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend.Models
{
    [Table("Accounts")]
    public class User
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("accountID")]
        public long AccountID { get; set; }

        [Required]
        [Column("username")]
        public string Username { get; set; } = string.Empty;

        [Required]
        [Column("email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [Column("password")]
        [StringLength(255)]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        [Column("unique key")]
        [StringLength(255)]
        public string UniqueKey { get; set; } = string.Empty;

        [Required]
        [Column("Database Name")]
        [StringLength(50)]
        public string DatabaseName { get; set; } = string.Empty;

        [Required]
        [Column("usernameHashed")]
        [StringLength(255)]
        public string UsernameHashed { get; set; } = string.Empty;

        [Required]
        [Column("emailHashed")]
        [StringLength(255)]
        public string EmailHashed { get; set; } = string.Empty;

        [Required]
        [Column("timezone")]
        [StringLength(50)]
        public string Timezone { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("last_login_at")]
        public DateTime? LastLoginAt { get; set; }
    }
}