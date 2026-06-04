using FluentAssertions;
using Template.Common.Configuration;

namespace Template.Common.UnitTests.Configuration;

/// <summary>
/// Environment stub that returns fixed values for both IsNative and IsProduction
/// without depending on real environment variables. The Settings class branches
/// on IsProduction() first, then on IsNative().
/// </summary>
file sealed class StubMyEnvironment(bool isProduction, bool isNative) : IMyEnvironment
{
    public bool IsProduction() => isProduction;
    public bool IsDevelopment() => !isProduction;
    public bool IsNative() => isNative;
    public bool IsWeb() => !isNative;
    public bool IsCutOff() => false;
    public string GetEnvironment() => isProduction ? MyEnvironment.Prod : MyEnvironment.Dev;
}

[TestClass]
public class SettingsTests
{
    [TestMethod]
    public void ApiUrl_WhenProduction_ReturnsProductionUrl()
    {
        var env = new StubMyEnvironment(isProduction: true, isNative: false);
        var settings = new Settings(env);

        settings.ApiUrl.ToString().Should().Be("https://your-app-api.azurewebsites.net/");
    }

    [TestMethod]
    public void ApiUrl_WhenNative_ReturnsAndroidEmulatorUrl()
    {
        // Even when production is true, IsNative is checked second only in the
        // current Settings implementation. Verify the actual precedence.
        // Per source: production is checked first, so a native+prod env returns prod.
        var env = new StubMyEnvironment(isProduction: false, isNative: true);
        var settings = new Settings(env);

        settings.ApiUrl.ToString().Should().Be("https://10.0.2.2:7224/");
    }

    [TestMethod]
    public void ApiUrl_WhenWebDevelopment_ReturnsDevelopmentUrl()
    {
        var env = new StubMyEnvironment(isProduction: false, isNative: false);
        var settings = new Settings(env);

        settings.ApiUrl.ToString().Should().Be("https://localhost:7224/");
    }

    [TestMethod]
    public void ApiUrl_ProductionTakesPrecedenceOverNative()
    {
        // The Settings implementation checks IsProduction() before IsNative(),
        // so a "native" production build would still get the production URL.
        var env = new StubMyEnvironment(isProduction: true, isNative: true);
        var settings = new Settings(env);

        settings.ApiUrl.ToString().Should().Be("https://your-app-api.azurewebsites.net/");
    }
}
