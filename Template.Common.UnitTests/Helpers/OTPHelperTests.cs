using FluentAssertions;
using Template.Common.Helpers;

namespace Template.Common.UnitTests.Helpers;

[TestClass]
public class OTPHelperTests
{
    [TestMethod]
    public void GenerateOTP_ReturnsExactly6Characters()
    {
        var otp = OTPHelper.GenerateOTP();

        otp.Should().HaveLength(6);
    }

    [TestMethod]
    public void GenerateOTP_ContainsOnlyDigits()
    {
        var otp = OTPHelper.GenerateOTP();

        otp.Should().MatchRegex(@"^\d{6}$");
    }

    [TestMethod]
    public void GenerateOTP_ValueIsInValidRange()
    {
        var otp = OTPHelper.GenerateOTP();

        var value = int.Parse(otp, System.Globalization.CultureInfo.InvariantCulture);
        value.Should().BeInRange(0, 999999);
    }

    [TestMethod]
    public void GenerateOTP_LeadingZeroesPreserved()
    {
        // Run enough times that a leading-zero code is statistically likely to appear,
        // and verify the format contract — always exactly 6 chars — holds for all of them.
        for (var i = 0; i < 200; i++)
        {
            OTPHelper.GenerateOTP().Should().HaveLength(6);
        }
    }

    [TestMethod]
    public void GenerateOTP_HasSufficientEntropy()
    {
        // Generate 100 OTPs and check we get at least 90 unique values (collision rate < 10%)
        var otps = Enumerable.Range(0, 100).Select(_ => OTPHelper.GenerateOTP()).ToList();

        otps.Distinct().Count().Should().BeGreaterThan(90);
    }
}
