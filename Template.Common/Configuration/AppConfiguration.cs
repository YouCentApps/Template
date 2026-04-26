namespace Template.Common.Configuration;

/// <summary>
/// Application configuration interface
/// </summary>
public interface IAppConfiguration
{
    Uri? ApiBaseUrl { get; }
    bool IsDevelopment { get; }
}

/// <summary>
/// Application configuration implementation
/// </summary>
public class AppConfiguration : IAppConfiguration
{
    public Uri? ApiBaseUrl { get; set; }
    public bool IsDevelopment { get; set; }
}
