namespace Template.Shared.Configuration;

/// <summary>
/// Settings interface for API URL resolution
/// </summary>
public interface ISettings
{
    string ApiUrl { get; }
}

/// <summary>
/// Settings implementation with environment-aware API URL resolution
/// </summary>
public class Settings : ISettings
{
    private readonly IMyEnvironment _environment;

    // TODO: Update these URLs for your deployment
    private const string ProductionApiUrl = "https://your-app-api.azurewebsites.net";
    private const string DevelopmentApiUrl = "https://localhost:7224";
    private const string AndroidEmulatorApiUrl = "https://10.0.2.2:7224";

    public Settings(IMyEnvironment environment)
    {
        _environment = environment;
    }

    public string ApiUrl
    {
        get
        {
            if (_environment.IsProduction())
            {
                return ProductionApiUrl;
            }

            // Android emulator requires special localhost routing
            // Note: DeviceInfo check requires MAUI context (will be overridden in Native project)
            if (_environment.IsNative())
            {
                // This will be properly implemented in the Native-specific MyEnvironment
                return AndroidEmulatorApiUrl;
            }

            return DevelopmentApiUrl;
        }
    }
}
