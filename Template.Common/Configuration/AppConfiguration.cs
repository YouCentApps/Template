namespace Template.Common.Configuration;

/// <summary>
/// Application configuration interface
/// </summary>
public interface IAppConfiguration
{
    string ApiBaseUrl { get; }
    bool IsDevelopment { get; }
}

/// <summary>
/// Application configuration implementation
/// </summary>
public class AppConfiguration : IAppConfiguration
{
    public string ApiBaseUrl { get; set; } = "https://localhost:7224";
    public bool IsDevelopment { get; set; } = true;
}
