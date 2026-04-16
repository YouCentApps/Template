namespace Template.Shared.Models;

/// <summary>
/// Authentication session entity for tracking user sessions
/// </summary>
public class AuthSession
{
    public string SessionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public DateTime ExpiryDate { get; set; }
    public bool IsActive { get; set; }
}
