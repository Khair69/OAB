using System.Globalization;
using System.Text;

namespace Oab.Core.Formatting;

/// <summary>
/// The inverse of <see cref="MoneyFormat"/>: turns what a shopkeeper typed into a
/// <see cref="decimal"/>. It lives in Core, beside the formatter, so the two
/// halves of the same problem stay together and both are unit-testable without a
/// MAUI runtime.
/// <para>
/// It exists because <see cref="MoneyFormat"/> can render <c>٥٠</c> but nothing
/// could read <c>٥٠</c> back. Every amount input in the app went through its own
/// copy of "try CurrentCulture, then InvariantCulture" — four copies of a parser
/// that could not read the digits the app itself prints. This is that function,
/// written once.
/// </para>
/// </summary>
public static class MoneyInput
{
    /// <summary>
    /// Parses an amount typed in any of the digit and separator forms an Arabic
    /// or English keyboard can produce.
    /// <para>
    /// Two passes: <paramref name="culture"/> (defaulting to
    /// <see cref="CultureInfo.CurrentCulture"/>), then
    /// <see cref="CultureInfo.InvariantCulture"/>. The second pass is what lets
    /// someone typing a plain ASCII <c>1250.50</c> succeed while the app is
    /// running in a culture whose decimal separator is <c>٫</c> — a phone's
    /// numeric keypad does not necessarily agree with the app's language.
    /// </para>
    /// <para>
    /// It parses; it does not judge. A negative or a zero comes back as
    /// <see langword="true"/> with that value, because "is this a number?" and
    /// "is this a sensible amount here?" are different questions with different
    /// answers per screen — a correction accepts zero (D19), a new purchase does
    /// not.
    /// </para>
    /// </summary>
    public static bool TryParseAmount(string? text, out decimal amount, CultureInfo? culture = null)
    {
        amount = 0m;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        return TryParseIn(text, culture ?? CultureInfo.CurrentCulture, out amount)
            || TryParseIn(text, CultureInfo.InvariantCulture, out amount);
    }

    private static bool TryParseIn(string text, CultureInfo culture, out decimal amount) =>
        decimal.TryParse(Normalize(text, culture), NumberStyles.Number, culture, out amount);

    /// <summary>
    /// Rewrites the typed text into something <see cref="decimal.TryParse"/> can
    /// read in <paramref name="culture"/>, without changing what it means.
    /// <para>
    /// Normalising per-culture rather than once to ASCII matters: the Arabic
    /// decimal separator becomes <em>this culture's</em> separator, so <c>١٢٣٫٥</c>
    /// survives both passes — as <c>123٫5</c> under a culture that uses <c>٫</c>,
    /// and as <c>123.5</c> under the invariant retry. Mapping it to a fixed
    /// <c>.</c> would break wherever <c>.</c> is a grouping separator, which is
    /// how a "1.5 becomes 15" bug gets written.
    /// </para>
    /// </summary>
    private static string Normalize(string text, CultureInfo culture)
    {
        var decimalSeparator = culture.NumberFormat.NumberDecimalSeparator;
        var builder = new StringBuilder(text.Length);

        foreach (var c in text)
        {
            switch (c)
            {
                // Arabic-Indic ٠١٢٣٤٥٦٧٨٩ — what MoneyFormat.MapDigits writes.
                case >= '٠' and <= '٩':
                    builder.Append((char)('0' + (c - '٠')));
                    break;

                // Extended Arabic-Indic ۰۱۲۳۴۵۶۷۸۹. Not what we render, but some
                // Android keyboards offer them and the user cannot tell the two
                // sets apart on screen.
                case >= '۰' and <= '۹':
                    builder.Append((char)('0' + (c - '۰')));
                    break;

                // ٫ ARABIC DECIMAL SEPARATOR.
                case '٫':
                    builder.Append(decimalSeparator);
                    break;

                // ٬ ARABIC THOUSANDS SEPARATOR and ، ARABIC COMMA — the comma key
                // on an Arabic keyboard. Both are grouping, never meaning, so
                // dropping them is safe and "١٬٥٠٠" reads as 1500.
                case '٬' or '،':
                    break;

                // LRM, RLM, ALM. An RTL text field can wrap what was typed in
                // these; they are invisible and would fail the parse. Written as
                // escapes deliberately — as literals they would be a blank gap
                // in the source that no reviewer could see.
                case '\u200E' or '\u200F' or '\u061C':
                    break;

                default:
                    builder.Append(c);
                    break;
            }
        }

        return builder.ToString();
    }
}
