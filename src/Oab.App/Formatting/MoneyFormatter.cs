using Oab.App.Localization;
using CoreMoneyFormat = Oab.Core.Formatting.MoneyFormat;

namespace Oab.App.Formatting;

public interface IMoneyFormatter
{
    /// <summary>"1,250.00 د.ج" — magnitude only; callers decide how to phrase direction.</summary>
    string Format(decimal amount);
}

/// <summary>
/// Binds the pure <see cref="CoreMoneyFormat"/> to this shop's config and the
/// current UI culture. All the actual formatting logic lives (and is tested) in Core.
/// </summary>
public class MoneyFormatter(ShopConfig config, LocalizationManager localization) : IMoneyFormatter
{
    public string Format(decimal amount) =>
        CoreMoneyFormat.Format(amount, localization.Culture, config.CurrencySymbol, config.UseArabicIndicDigits);
}
