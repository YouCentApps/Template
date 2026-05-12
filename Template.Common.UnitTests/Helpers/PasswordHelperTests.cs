using FluentAssertions;
using Template.Common.Helpers;

namespace Template.Common.UnitTests.Helpers;

[TestClass]
public class PasswordHelperTests
{
    [TestMethod]
    public void GenerateSalt_ReturnsNonEmptyBase64String()
    {
        var salt = PasswordHelper.GenerateSalt();

        salt.Should().NotBeNullOrWhiteSpace();
        Convert.TryFromBase64String(salt, new byte[64], out _).Should().BeTrue();
    }

    [TestMethod]
    public void GenerateSalt_Returns32ByteSalt()
    {
        var salt = PasswordHelper.GenerateSalt();

        Convert.FromBase64String(salt).Should().HaveCount(32);
    }

    [TestMethod]
    public void GenerateSalt_TwoCallsReturnDifferentValues()
    {
        var salt1 = PasswordHelper.GenerateSalt();
        var salt2 = PasswordHelper.GenerateSalt();

        salt1.Should().NotBe(salt2);
    }

    [TestMethod]
    public void HashPassword_SamePasswordAndSalt_ReturnsSameHash()
    {
        var salt = PasswordHelper.GenerateSalt();

        var hash1 = PasswordHelper.HashPassword("MyPassword1!", salt);
        var hash2 = PasswordHelper.HashPassword("MyPassword1!", salt);

        hash1.Should().Be(hash2);
    }

    [TestMethod]
    public void HashPassword_SamePasswordDifferentSalts_ReturnsDifferentHashes()
    {
        var salt1 = PasswordHelper.GenerateSalt();
        var salt2 = PasswordHelper.GenerateSalt();

        var hash1 = PasswordHelper.HashPassword("MyPassword1!", salt1);
        var hash2 = PasswordHelper.HashPassword("MyPassword1!", salt2);

        hash1.Should().NotBe(hash2);
    }

    [TestMethod]
    public void HashPassword_DifferentPasswords_ReturnsDifferentHashes()
    {
        var salt = PasswordHelper.GenerateSalt();

        var hash1 = PasswordHelper.HashPassword("PasswordA1!", salt);
        var hash2 = PasswordHelper.HashPassword("PasswordB2@", salt);

        hash1.Should().NotBe(hash2);
    }

    [TestMethod]
    public void HashPassword_ReturnsValidBase64String()
    {
        var salt = PasswordHelper.GenerateSalt();
        var hash = PasswordHelper.HashPassword("MyPassword1!", salt);

        hash.Should().NotBeNullOrWhiteSpace();
        Convert.TryFromBase64String(hash, new byte[64], out _).Should().BeTrue();
    }

    [TestMethod]
    public void VerifyPassword_CorrectPassword_ReturnsTrue()
    {
        var salt = PasswordHelper.GenerateSalt();
        var hash = PasswordHelper.HashPassword("Correct1Pass!", salt);

        PasswordHelper.VerifyPassword("Correct1Pass!", hash, salt).Should().BeTrue();
    }

    [TestMethod]
    public void VerifyPassword_WrongPassword_ReturnsFalse()
    {
        var salt = PasswordHelper.GenerateSalt();
        var hash = PasswordHelper.HashPassword("Correct1Pass!", salt);

        PasswordHelper.VerifyPassword("WrongPass1!", hash, salt).Should().BeFalse();
    }

    [TestMethod]
    public void VerifyPassword_TamperedHash_ReturnsFalse()
    {
        var salt = PasswordHelper.GenerateSalt();
        var hash = PasswordHelper.HashPassword("Correct1Pass!", salt);
        var tampered = hash[..^4] + "XXXX";

        PasswordHelper.VerifyPassword("Correct1Pass!", tampered, salt).Should().BeFalse();
    }

    [TestMethod]
    public void VerifyPassword_WrongSalt_ReturnsFalse()
    {
        var salt1 = PasswordHelper.GenerateSalt();
        var salt2 = PasswordHelper.GenerateSalt();
        var hash = PasswordHelper.HashPassword("Correct1Pass!", salt1);

        PasswordHelper.VerifyPassword("Correct1Pass!", hash, salt2).Should().BeFalse();
    }
}
