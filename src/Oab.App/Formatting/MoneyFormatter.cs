using Oab.App.Localization;

namespace Oab.App.Formatting;

public interface IMoneyFormatter
{
    /// <summary>"1,250.00 د.ج" — magnitude only; callers decide how to phrase direction.</summary>
    string Format(decimal amount);
}

public class MoneyFormatter(ShopConfig config, LocalizationManager localization) : IMoneyFormatter
{
    private static readonly char[] ArabicIndicDigits =
        ['٠', '١', '٢', '٣', '٤', '٥', '٦', '٧', '٨', '٩'];

    public string Format(decimal amount)
    {
        var text = Math.Abs(amount).ToString("N2", localization.Culture);
        if (config.UseArabicIndicDigits)
            text = MapDigits(text);
        return config.CurrencySymbol.Length == 0 ? text : $"{text} {config.CurrencySymbol}";
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
