namespace Template.Common.Helpers;

/// <summary>
/// Application-wide validation policies
/// </summary>
public static class ValidationPolicies
{
    // Password policies
    public const int MinimumPasswordLength = 8;
    public const int MaximumPasswordLength = 40;
    public const string PasswordRegex = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[!@#$%^&*]).*$";

    // Username policies
    public const int MinimumUsernameLength = 3;
    public const int MaximumUsernameLength = 50;
    public const string UsernameRegex = @"^[a-zA-Z0-9_]+$";

    // Email validation
    public const string EmailRegex = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";
}
