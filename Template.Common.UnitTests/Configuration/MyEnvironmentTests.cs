using FluentAssertions;
using Template.Common.Configuration;

namespace Template.Common.UnitTests.Configuration;

/// <summary>
/// In-process environment that lets the test author set the environment name
/// directly, without mutating the real ASPNETCORE_ENVIRONMENT variable.
/// </summary>
file sealed class TestableMyEnvironment(string envName) : MyEnvironment
{
    private readonly string _envName = envName;
    public override string GetEnvironment() => _envName;
}

[TestClass]
public class MyEnvironmentTests
{
    [TestMethod]
    public void IsDevelopment_WhenEnvIsDevelopment_ReturnsTrue()
    {
        var env = new TestableMyEnvironment(MyEnvironment.Dev);

        env.IsDevelopment().Should().BeTrue();
    }

    [TestMethod]
    public void IsDevelopment_WhenEnvIsProduction_ReturnsFalse()
    {
        var env = new TestableMyEnvironment(MyEnvironment.Prod);

        env.IsDevelopment().Should().BeFalse();
    }

    [TestMethod]
    public void IsProduction_WhenEnvIsProduction_ReturnsTrue()
    {
        var env = new TestableMyEnvironment(MyEnvironment.Prod);

        env.IsProduction().Should().BeTrue();
    }

    [TestMethod]
    public void IsProduction_WhenEnvIsDevelopment_ReturnsFalse()
    {
        var env = new TestableMyEnvironment(MyEnvironment.Dev);

        env.IsProduction().Should().BeFalse();
    }

    [TestMethod]
    public void BaseDefaults_AreWebNotNativeNotCutOff()
    {
        var env = new TestableMyEnvironment(MyEnvironment.Dev);

        // The base MyEnvironment is the web/server-side implementation.
        env.IsWeb().Should().BeTrue();
        env.IsNative().Should().BeFalse();
        env.IsCutOff().Should().BeFalse();
    }

    [TestMethod]
    public void GetEnvironment_WhenAspnetcoreEnvSet_ReturnsIt()
    {
        const string previous = "ASPNETCORE_ENVIRONMENT";
        var original = Environment.GetEnvironmentVariable(previous);
        try
        {
            Environment.SetEnvironmentVariable(previous, MyEnvironment.Prod);

            var env = new MyEnvironment();

            env.GetEnvironment().Should().Be(MyEnvironment.Prod);
        }
        finally
        {
            Environment.SetEnvironmentVariable(previous, original);
        }
    }

    [TestMethod]
    public void GetEnvironment_WhenAspnetcoreEnvUnset_ReturnsUnknown()
    {
        const string previous = "ASPNETCORE_ENVIRONMENT";
        var original = Environment.GetEnvironmentVariable(previous);
        try
        {
            Environment.SetEnvironmentVariable(previous, null);

            var env = new MyEnvironment();

            env.GetEnvironment().Should().Be(MyEnvironment.Unknown);
        }
        finally
        {
            Environment.SetEnvironmentVariable(previous, original);
        }
    }
}
