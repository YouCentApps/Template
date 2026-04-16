namespace Template.Shared.Helpers;

/// <summary>
/// Azure Table Storage table name constants
/// TODO: Update table names with your app prefix
/// </summary>
public static class TableNames
{
    public const string Users = "TemplateUsers";
    public const string AuthSessions = "TemplateAuthSessions";
    public const string OTPCodes = "TemplateOTPCodes";
    public const string Admins = "TemplateAdmins";
    
    // Add more table names as your app grows
    // public const string YourFeature = "TemplateYourFeature";
}
