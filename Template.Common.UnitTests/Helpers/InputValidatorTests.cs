using FluentAssertions;
using Template.Common.Helpers;

namespace Template.Common.UnitTests.Helpers;

[TestClass]
public class InputValidatorTests
{
    // ── NormalizeEmail ────────────────────────────────────────────────────────

    [TestMethod]
    public void NormalizeEmail_TrimsWhitespaceAndUppercases()
    {
        InputValidator.NormalizeEmail("  test@Example.COM  ").Should().Be("TEST@EXAMPLE.COM");
    }

    [TestMethod]
    public void NormalizeEmail_AlreadyUppercase_ReturnsSameValue()
    {
        InputValidator.NormalizeEmail("USER@DOMAIN.COM").Should().Be("USER@DOMAIN.COM");
    }

    [TestMethod]
    public void NormalizeEmail_NullInput_ReturnsEmpty()
    {
        InputValidator.NormalizeEmail(null).Should().BeEmpty();
    }

    [TestMethod]
    public void NormalizeEmail_EmptyString_ReturnsEmpty()
    {
        InputValidator.NormalizeEmail(string.Empty).Should().BeEmpty();
    }

    [TestMethod]
    public void NormalizeEmail_WhitespaceOnly_ReturnsEmpty()
    {
        InputValidator.NormalizeEmail("   ").Should().BeEmpty();
    }

    // ── NormalizeUsername ─────────────────────────────────────────────────────

    [TestMethod]
    public void NormalizeUsername_TrimsWhitespaceAndUppercases()
    {
        InputValidator.NormalizeUsername("  johndoe  ").Should().Be("JOHNDOE");
    }

    [TestMethod]
    public void NormalizeUsername_MixedCase_ReturnsUppercase()
    {
        InputValidator.NormalizeUsername("JohnDoe").Should().Be("JOHNDOE");
    }

    [TestMethod]
    public void NormalizeUsername_NullInput_ReturnsEmpty()
    {
        InputValidator.NormalizeUsername(null).Should().BeEmpty();
    }

    [TestMethod]
    public void NormalizeUsername_EmptyString_ReturnsEmpty()
    {
        InputValidator.NormalizeUsername(string.Empty).Should().BeEmpty();
    }

    [TestMethod]
    public void NormalizeUsername_WhitespaceOnly_ReturnsEmpty()
    {
        InputValidator.NormalizeUsername("   ").Should().BeEmpty();
    }

    // ── GenerateUserId ────────────────────────────────────────────────────────

    [TestMethod]
    public void GenerateUserId_Returns32CharString()
    {
        InputValidator.GenerateUserId().Should().HaveLength(32);
    }

    [TestMethod]
    public void GenerateUserId_ContainsNoHyphens()
    {
        InputValidator.GenerateUserId().Should().NotContain("-");
    }

    [TestMethod]
    public void GenerateUserId_IsLowerHex()
    {
        InputValidator.GenerateUserId().Should().MatchRegex(@"^[0-9a-f]{32}$");
    }

    [TestMethod]
    public void GenerateUserId_TwoCallsReturnDifferentValues()
    {
        InputValidator.GenerateUserId().Should().NotBe(InputValidator.GenerateUserId());
    }
}
