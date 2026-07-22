using System.Globalization;
using Oab.Core.Formatting;

namespace Oab.Core.Tests;

/// <summary>
/// The parser's contract, stated as tests. The round-trip cases at the bottom
/// are the point of the class: anything <see cref="MoneyFormat"/> can print,
/// <see cref="MoneyInput"/> must be able to read back.
/// </summary>
public class MoneyInputTests
{
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo Ar = CultureInfo.GetCultureInfo("ar");

    private static decimal Parse(string text, CultureInfo culture)
    {
        Assert.True(MoneyInput.TryParseAmount(text, out var amount, culture),
            $"'{text}' did not parse under {culture.Name}");
        return amount;
    }

    // --- What already worked, kept working ------------------------------------

    [Theory]
    [InlineData("50", 50)]
    [InlineData("1250.50", 1250.50)]
    [InlineData("1,250.50", 1250.50)]
    [InlineData("  42  ", 42)]
    [InlineData("0", 0)]
    public void AsciiDigits_ParseAsBefore(string text, decimal expected) =>
        Assert.Equal(expected, Parse(text, En));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("١٢غ")]
    public void Nonsense_IsRejected(string text) =>
        Assert.False(MoneyInput.TryParseAmount(text, out _, En));

    [Fact]
    public void Null_IsRejected() =>
        Assert.False(MoneyInput.TryParseAmount(null, out _, En));

    [Fact]
    public void RejectedInput_LeavesAmountAtZero()
    {
        Assert.False(MoneyInput.TryParseAmount("abc", out var amount, En));
        Assert.Equal(0m, amount);
    }

    // --- The gap this class closes -------------------------------------------

    [Theory]
    [InlineData("٥٠", 50)]
    [InlineData("١٢٣", 123)]
    [InlineData("٠", 0)]
    [InlineData("١٢٣٤٥٦٧٨٩٠", 1234567890)]
    public void ArabicIndicDigits_Parse(string text, decimal expected) =>
        Assert.Equal(expected, Parse(text, En));

    /// <summary>Some Android keyboards offer the Persian forms instead.</summary>
    [Theory]
    [InlineData("۵۰", 50)]
    [InlineData("۱۲۳", 123)]
    public void ExtendedArabicIndicDigits_Parse(string text, decimal expected) =>
        Assert.Equal(expected, Parse(text, En));

    [Fact]
    public void MixedDigitSets_Parse() =>
        // Half-typed with one keyboard, half with another. It is one number.
        Assert.Equal(123m, Parse("١2٣", En));

    // --- Separators -----------------------------------------------------------

    [Theory]
    [InlineData("١٢٣٫٥", 123.5)]
    [InlineData("123٫5", 123.5)]
    public void ArabicDecimalSeparator_Parses(string text, decimal expected) =>
        Assert.Equal(expected, Parse(text, En));

    [Theory]
    [InlineData("١٬٥٠٠", 1500)]
    [InlineData("١،٥٠٠", 1500)]
    public void ArabicGroupSeparators_AreDropped(string text, decimal expected) =>
        // ٬ is the real thousands separator; ، is the comma key an Arabic
        // keyboard actually offers. Both mean grouping, never a decimal point.
        Assert.Equal(expected, Parse(text, En));

    [Fact]
    public void ArabicDecimalSeparator_Parses_UnderArabicCulture() =>
        // The pass that matters most: the app runs under `ar`, and whether that
        // culture's own decimal separator is '.' or '٫' is an ICU detail this
        // must not depend on.
        Assert.Equal(123.5m, Parse("١٢٣٫٥", Ar));

    [Fact]
    public void AsciiDecimalPoint_Parses_UnderArabicCulture() =>
        // A phone's numeric keypad prints '.' regardless of the app's language.
        // This is what the invariant second pass is for.
        Assert.Equal(1250.50m, Parse("1250.50", Ar));

    [Fact]
    public void BidiMarks_AreIgnored()
    {
        // What an RTL entry field can hand back: RLM + digits + RLM.
        var wrapped = "\u200F٥٠\u200F";
        Assert.Equal(50m, Parse(wrapped, Ar));
    }

    // --- Deliberately not the parser's job ------------------------------------

    [Fact]
    public void Negative_Parses_AndIsTheCallersToReject() =>
        // "Is this a number?" and "is this a sensible amount here?" are separate
        // questions — a correction accepts 0, a new purchase does not.
        Assert.Equal(-5m, Parse("-5", En));

    // --- Round trip -----------------------------------------------------------

    [Theory]
    [InlineData(50)]
    [InlineData(1250.50)]
    [InlineData(1234567.89)]
    [InlineData(0)]
    public void AnythingMoneyFormatPrints_ReadsBack(decimal amount)
    {
        var printed = MoneyFormat.Format(amount, En, useArabicIndicDigits: true);
        Assert.Equal(amount, Parse(printed, En));
    }

    [Theory]
    [InlineData(50)]
    [InlineData(1250.50)]
    [InlineData(1234567.89)]
    public void TheShippingConfiguration_RoundTrips(decimal amount)
    {
        // `ar` + Arabic-Indic digits is what a Syrian shop actually runs, and it
        // is the harshest case: .NET's `ar` uses ٫ for the decimal point and ٬
        // for grouping, so 1250.50 prints as "١٬٢٥٠٫٥٠" — not one ASCII
        // character in it. This assertion is the feature.
        var printed = MoneyFormat.Format(amount, Ar, useArabicIndicDigits: true);
        Assert.Equal(amount, Parse(printed, Ar));
    }

    [Fact]
    public void ArabicSeparators_ParseUnderEnglishToo() =>
        // The language switch is live (D18), so an amount can be read in one
        // culture and retyped in the other. The separators must not be tied to
        // whichever language happens to be on at the time.
        Assert.Equal(1250.50m, Parse("١٬٢٥٠٫٥٠", En));

    [Fact]
    public void CurrencySymbol_IsNotParsed() =>
        // Stated as a test because it is a real limitation, not an oversight:
        // a currency symbol is decoration the formatter adds, and the parser
        // reads what was *typed* into an amount box, which never contains one.
        Assert.False(MoneyInput.TryParseAmount(
            MoneyFormat.Format(50m, En, "SP"), out _, En));
}
