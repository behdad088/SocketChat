using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Identity.API.Models;

namespace Identity.API.Data;

public class VerificationCode
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; } = null!;

    [MaxLength(100)]
    public required string UserId { get; set; }
    
    [Required]
    [MaxLength(64)]
    public required string Code { get; set; }

    [MaxLength(500)]
    public required string Type { get; set; }
    
    public DateTime CreatedAt { get; set; }

    public bool IsActivated { get; set; } = false;
    
    public ApplicationUser User { get; set; } = null!;
    
    public bool IsExpired => 
        CreatedAt.AddMinutes(30) < DateTime.UtcNow; // Assuming 30 minutes expiration time
}