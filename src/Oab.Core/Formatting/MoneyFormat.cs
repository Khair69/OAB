using System.Globalization;

namespace Oab.Core.Formatting;

/// <summary>
/// Pure money-to-string formatting, with no dependency on the UI framework.
/// It lives in Core so the digit-shaping and currency rules can be unit-tested
/// without spinning up a MAUI runtime.
/// </summary>
public static class MoneyFormat
{
    private static readonly char[] ArabicIndicDigits =
        ['٠', '١', '٢', '٣', '٤', '٥', '٦', '٧', '٨', '٩'];

    /// <summary>
    /// Formats the <b>magnitude</b> of <paramref name="amount"/> with two
    /// decimals in <paramref name="culture"/>, optionally shaping the digits to
    /// Arabic-Indic and appending a currency symbol. The sign is intentionally
    /// dropped — callers phrase direction in words ("you owe" / "owes you").
    /// </summary>
    public static string Format(
        decimal amount,
        CultureInfo culture,
        string currencySymbol = "",
        bool useArabicIndicDigits = false)
    {
        var text = Math.Abs(amount).ToString("N2", culture);
        if (useArabicIndicDigits)
            text = MapDigits(text);
        return string.IsNullOrEmpty(currencySymbol) ? text : $"{text} {currencySymbol}";
    }

    // .NET number formatting ignores NativeDigits, so shape the digits ourselves.
    private static string MapDigits(string text)
    {
        var chars = text.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (chars[i] is >= '0' and <= '9')
                chars[i] = ArabicIndicDigits[chars[i] - '0'];
        }
        return new string(chars);
    }
}
