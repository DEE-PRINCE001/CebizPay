using CebizPay.Application.Common.Utils;
using CebizPay.Domain.Vas.Enums;
using Xunit;

namespace CebizPay.UnitTests.Vas;

public class PhoneNormalizerTests
{
    [Theory]
    [InlineData("08031234567", "08031234567")]
    [InlineData("+2348031234567", "08031234567")]
    [InlineData("2348031234567", "08031234567")]
    [InlineData("0803 123 4567", "08031234567")]
    [InlineData("  0803-123-4567  ", "08031234567")]
    public void NormalizeNational_FormatsToStandard11DigitNational(string input, string expected)
    {
        var normalized = PhoneNormalizer.NormalizeNational(input);
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("08031234567", "2348031234567")]
    [InlineData("+2348031234567", "2348031234567")]
    [InlineData("2348031234567", "2348031234567")]
    public void NormalizeInternational_FormatsToStandard13Digits(string input, string expected)
    {
        var normalized = PhoneNormalizer.NormalizeInternational(input);
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("08031234567", true)]
    [InlineData("07011234567", true)]
    [InlineData("09091234567", true)]
    [InlineData("08051234567", true)]
    [InlineData("12345", false)]
    [InlineData("0803123456", false)]
    [InlineData("080312345678", false)]
    [InlineData("abcdefghijk", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidNigerianPhoneNumber_ValidatesFormat(string? input, bool expected)
    {
        var isValid = PhoneNormalizer.IsValidNigerianPhoneNumber(input);
        Assert.Equal(expected, isValid);
    }

    [Theory]
    [InlineData("08031234567", VasNetwork.Mtn)]
    [InlineData("08061234567", VasNetwork.Mtn)]
    [InlineData("07031234567", VasNetwork.Mtn)]
    [InlineData("08021234567", VasNetwork.Airtel)]
    [InlineData("08081234567", VasNetwork.Airtel)]
    [InlineData("07081234567", VasNetwork.Airtel)]
    [InlineData("08051234567", VasNetwork.Glo)]
    [InlineData("08071234567", VasNetwork.Glo)]
    [InlineData("07051234567", VasNetwork.Glo)]
    [InlineData("08091234567", VasNetwork.NineMobile)]
    [InlineData("08171234567", VasNetwork.NineMobile)]
    [InlineData("08181234567", VasNetwork.NineMobile)]
    public void DetectNetworkFromPrefix_ResolvesCorrectCarrier(string phone, VasNetwork expectedNetwork)
    {
        var detected = PhoneNormalizer.DetectNetworkFromPrefix(phone);
        Assert.Equal(expectedNetwork, detected);
    }
}
