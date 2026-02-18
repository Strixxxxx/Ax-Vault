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
        public string Username { get; set; } = string.Empty; // Encrypted

        [Required]
        [Column("email")]
        public string Email { get; set; } = string.Empty; // Encrypted

        [Required]
        [Column("password")]
        [StringLength(255)]
        public string PasswordHash { get; set; } = string.Empty; // Argon2id Hash of Account Password

        [Required]
        [Column("random verifier")]
        [StringLength(255)]
        public string RandomVerifier { get; set; } = string.Empty; // Encrypted Random Verifier

        // Note: No Vault Key or Vault Password is stored here.

        [Required]
        [Column("usernameHashed")]
        [StringLength(255)]
        public string UsernameHashed { get; set; } = string.Empty; // Deterministic Hash for Lookup

        [Required]
        [Column("emailHashed")]
        [StringLength(255)]
        public string EmailHashed { get; set; } = string.Empty; // Deterministic Hash for Lookup

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
