using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend.Models
{
    [Table("Vaults")]
    public class Vault
    {
        [Key]
        [Column("VaultId")]
        public Guid VaultId { get; set; } = Guid.NewGuid();

        [Required]
        [Column("accountID")]
        public long AccountID { get; set; }

        [Required]
        [Column("Platform")]
        [StringLength(100)]
        public string Platform { get; set; } = string.Empty;

        [ForeignKey("AccountID")]
        public virtual User? User { get; set; }
    }
}
