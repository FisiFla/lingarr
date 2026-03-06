using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lingarr.Core.Entities;

[Table("service_quotas")]
public class ServiceQuota
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string ServiceType { get; set; } = string.Empty;

    public long? MonthlyLimitChars { get; set; }

    public long CharsUsed { get; set; }

    public int ResetMonth { get; set; }
}
