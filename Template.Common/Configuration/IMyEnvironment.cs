namespace Template.Common.Configuration;

/// <summary>
/// Interface for determining the current execution environment
/// </summary>
public interface IMyEnvironment
{
    /// <summary>
    /// Check if running in development mode
    /// </summary>
    bool IsDevelopment();

    /// <summary>
    /// Check if running in production mode
    /// </summary>
    bool IsProduction();

    /// <summary>
    /// Get the current environment name
    /// </summary>
    string GetEnvironment();

    /// <summary>
    /// Check if running as a native MAUI app
    /// </summary>
    bool IsNative();

    /// <summary>
    /// Check if running as a web application
    /// </summary>
    bool IsWeb();

    /// <summary>
    /// Check if network connectivity is unavailable (MAUI only)
    /// </summary>
    bool IsCutOff();
}
