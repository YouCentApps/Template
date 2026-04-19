namespace Template.Common.Configuration;

/// <summary>
/// Base environment detection implementation for web/API projects
/// </summary>
public class MyEnvironment : IMyEnvironment
{
    public const string Prod = "Production";
    public const string Dev = "Development";
    public const string Unknown = "Unknown";
    public const string MauiEnvironment = "MAUI_ENVIRONMENT";

    public virtual bool IsNative() => false;
    public virtual bool IsWeb() => true;
    public virtual bool IsCutOff() => false;

    public bool IsDevelopment()
    {
        return GetEnvironment() == Dev;
    }

    public bool IsProduction()
    {
        return GetEnvironment() == Prod;
    }

    public virtual string GetEnvironment()
    {
        return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? Unknown;
    }
}
