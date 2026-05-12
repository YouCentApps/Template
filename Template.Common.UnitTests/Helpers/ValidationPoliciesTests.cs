using FluentAssertions;
using System.Text.RegularExpressions;
using Template.Common.Helpers;

namespace Template.Common.UnitTests.Helpers;

[TestClass]
public class ValidationPoliciesTests
{
    // ── Password policy ───────────────────────────────────────────────────────

    [TestMethod]
    [DataRow("Short1!")]          // too short (7 chars)
    [DataRow("nouppercase1!")]    // no uppercase
    [DataRow("NOLOWERCASE1!")]    // no lowercase
    [DataRow("NoDigitHere!")]     // no digit
    [DataRow("NoSpecial1a")]      // no special char
    public void PasswordRegex_InvalidPasswords_DoNotMatch(string password)
    {
        Regex.IsMatch(password, ValidationPolicies.PasswordRegex).Should().BeFalse(
            because: $"'{password}' should fail password policy");
    }

    [TestMethod]
    [DataRow("ValidPass1!")]
    [DataRow("Another@2Secure")]
    [DataRow("Complex#3Password")]
    public void PasswordRegex_ValidPasswords_Match(string password)
    {
        Regex.IsMatch(password, ValidationPolicies.PasswordRegex).Should().BeTrue(
            because: $"'{password}' should pass password policy");
    }

    [TestMethod]
    public void PasswordPolicy_MinLength_Is8()
    {
        ValidationPolicies.MinimumPasswordLength.Should().Be(8);
    }

    [TestMethod]
    public void PasswordPolicy_MaxLength_Is40()
    {
        ValidationPolicies.MaximumPasswordLength.Should().Be(40);
    }

    // ── Username policy ───────────────────────────────────────────────────────

    [TestMethod]
    [DataRow("valid_user")]
    [DataRow("User123")]
    [DataRow("ABC")]
    public void UsernameRegex_ValidUsernames_Match(string username)
    {
        Regex.IsMatch(username, ValidationPolicies.UsernameRegex).Should().BeTrue(
            because: $"'{username}' should be a valid username");
    }

    [TestMethod]
    [DataRow("has space")]
    [DataRow("has-dash")]
    [DataRow("has@symbol")]
    public void UsernameRegex_InvalidUsernames_DoNotMatch(string username)
    {
        Regex.IsMatch(username, ValidationPolicies.UsernameRegex).Should().BeFalse(
            because: $"'{username}' should fail username policy");
    }

    [TestMethod]
    public void UsernamePolicy_MinLength_Is3()
    {
        ValidationPolicies.MinimumUsernameLength.Should().Be(3);
    }

    [TestMethod]
    public void UsernamePolicy_MaxLength_Is50()
    {
        ValidationPolicies.MaximumUsernameLength.Should().Be(50);
    }

    // ── Email policy ──────────────────────────────────────────────────────────

    [TestMethod]
    [DataRow("user@example.com")]
    [DataRow("user.name+tag@domain.co.uk")]
    public void EmailRegex_ValidEmails_Match(string email)
    {
        Regex.IsMatch(email, ValidationPolicies.EmailRegex).Should().BeTrue(
            because: $"'{email}' should be a valid email");
    }

    [TestMethod]
    [DataRow("notanemail")]
    [DataRow("missing@tld")]
    [DataRow("@nodomain.com")]
    public void EmailRegex_InvalidEmails_DoNotMatch(string email)
    {
        Regex.IsMatch(email, ValidationPolicies.EmailRegex).Should().BeFalse(
            because: $"'{email}' should fail email policy");
    }
}
