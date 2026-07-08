namespace Oab.App;

/// <summary>
/// Everything about one shop that should be configuration, not code. A new
/// customer build starts by filling one of these in their head project —
/// features come from which modules they reference, wording and language
/// come from here.
/// </summary>
public class ShopConfig
{
    public required string ShopName { get; init; }

    /// <summary>Shown after amounts, e.g. "د.ج" or "SP". Empty = numbers only.</summary>
    public string CurrencySymbol { get; init; } = "";

    /// <summary>Culture used on first launch, e.g. "ar". The user can switch later.</summary>
    public string DefaultCulture { get; init; } = "ar";

    /// <summary>Cultures the in-app language switcher cycles through.</summary>
    public IReadOnlyList<string> SupportedCultures { get; init; } = ["ar", "en"];

    /// <summary>Render amounts with Arabic-Indic digits (٠١٢٣...) instead of 0123.</summary>
    public bool UseArabicIndicDigits { get; init; }

    /// <summary>
    /// Per-shop wording. Key is a resource key from Strings.resx, optionally
    /// prefixed with a culture ("ar:Purchases_Title") to override only that
    /// language. This is how one shop calls the ledger "دفتر" and another "حساب"
    /// without touching code.
    /// </summary>
    public IReadOnlyDictionary<string, string> LabelOverrides { get; init; } =
        new Dictionary<string, string>();

    /// <summary>SQLite file name inside the app's private data directory.</summary>
    public string DatabaseFileName { get; init; } = "oab.db";
}
