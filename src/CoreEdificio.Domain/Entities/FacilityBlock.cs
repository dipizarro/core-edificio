using System.ComponentModel.DataAnnotations;

namespace CoreEdificio.Domain.Entities;

public class FacilityBlock
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CommunityId { get; set; }
    public Guid FacilityId { get; set; }
    
    [Required]
    public DateTime StartAtUtc { get; set; }
    
    [Required]
    public DateTime EndAtUtc { get; set; }
    
    [Required]
    [MinLength(3)]
    public string Reason { get; set; } = null!;
    
    public bool IsActive { get; set; } = true;
    
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation property (optional, but good for EF)
    public virtual Facility Facility { get; set; } = null!;
}
