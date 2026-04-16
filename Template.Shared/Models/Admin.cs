namespace Template.Shared.Models;

/// <summary>
/// Admin entity for managing administrative privileges
/// </summary>
public class Admin
{
    public string UserId { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool CanManageAdmins { get; set; }
    public DateTime CreatedDate { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? LastModifiedDate { get; set; }
    public string? LastModifiedBy { get; set; }
}
