using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend.Models
{
    [Table("Sessions")]
    public class Session
    {
        [Key]
        [Column("SessionId")]
        public long SessionId { get; set; }

        [Required]
        [Column("accountID")]
        public long AccountID { get; set; }

        [Required]
        [Column("LoginTime")]
        public DateTime LoginTime { get; set; }

        [Column("LogoutTime")]
        public DateTime? LogoutTime { get; set; }

        [Column("IPAddress")]
        [StringLength(45)]
        public string? IPAddress { get; set; }

        [Column("UserAgent")]
        [StringLength(255)]
        public string? UserAgent { get; set; }

        [ForeignKey("AccountID")]
        public virtual User? User { get; set; }
    }
}
