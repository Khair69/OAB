using System.Globalization;
using Oab.Core.Formatting;

namespace Oab.Core.Tests;

public class MoneyFormatTests
{
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");

    [Fact]
    public void FormatsTwoDecimals_WithGroupingAndSymbol() =>
        Assert.Equal("1,250.00 SP", MoneyFormat.Format(1250m, En, "SP"));

    [Fact]
    public void DropsSign_MagnitudeOnly() =>
        Assert.Equal("500.00", MoneyFormat.Format(-500m, En));

    [Fact]
    public void NoSymbol_NumberOnly() =>
        Assert.Equal("42.50", MoneyFormat.Format(42.5m, En));

    [Fact]
    public void ArabicIndicDigits_AreShaped() =>
        // 123.00 with eastern-Arabic digits; the decimal point stays ASCII.
        Assert.Equal("١٢٣.٠٠", MoneyFormat.Format(123m, En, useArabicIndicDigits: true));

    [Fact]
    public void ArabicIndicDigits_WithSymbol() =>
        Assert.Equal("١٠.٠٠ د.ج", MoneyFormat.Format(10m, En, "د.ج", useArabicIndicDigits: true));
}
