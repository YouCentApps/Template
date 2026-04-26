namespace Template.Common.Configuration;

/// <summary>
/// Settings interface for API URL resolution
/// </summary>
public interface ISettings
{
    Uri ApiUrl { get; }
}

/// <summary>
/// Settings implementation with environment-aware API URL resolution
/// </summary>
public class Settings(IMyEnvironment environment) : ISettings
{
    private readonly IMyEnvironment _environment = environment;

    // TODO: Update these URLs for your deployment
    private static readonly Uri ProductionApiUrl = new("https://your-app-api.azurewebsites.net");
    private static readonly Uri DevelopmentApiUrl = new("https://localhost:7224");
    private static readonly Uri AndroidEmulatorApiUrl = new("https://10.0.2.2:7224");

    public Uri ApiUrl
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
