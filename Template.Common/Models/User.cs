namespace Template.Common.Models;

/// <summary>
/// User entity for storing user accounts
/// </summary>
public class User
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string NormalizedUsername { get; set; } = string.Empty; // Lowercase username for lookups
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;
    public string PreferredAuthMethod { get; set; } = "Both"; // "Email", "Password", "Both"
    public bool IsActive { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? LastLoginDate { get; set; }
}
