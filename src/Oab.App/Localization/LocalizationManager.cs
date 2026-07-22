using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace Oab.App.Localization;

/// <summary>
/// Single source of truth for display text. Lookup order:
/// shop label override (culture-specific, then general) → Strings.resx for the
/// current culture → the key itself. Raising PropertyChanged on the indexer
/// makes every {oab:Tr} binding in the app re-resolve, which is how language
/// switching works live, without restarting.
/// </summary>
public class LocalizationManager : INotifyPropertyChanged
{
    private const string CulturePreferenceKey = "oab.culture";

    private static readonly ResourceManager Resources =
        new("Oab.App.Resources.Strings", typeof(LocalizationManager).Assembly);

    private readonly ShopConfig _config;
    private readonly IPreferences _preferences;

    /// <summary>Set once by UseOab so XAML markup extensions can reach the instance.</summary>
    public static LocalizationManager? Current { get; internal set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public CultureInfo Culture { get; private set; }

    /// <summary>
    /// <paramref name="preferences"/> is where the chosen language is persisted
    /// (normally <c>Preferences.Default</c>); it is injected so this class — and
    /// every view model that depends on it — can be unit-tested without a device.
    /// </summary>
    public LocalizationManager(ShopConfig config, IPreferences preferences)
    {
        _config = config;
        _preferences = preferences;
        var saved = preferences.Get(CulturePreferenceKey, config.DefaultCulture);
        Culture = SafeCulture(saved) ?? SafeCulture(config.DefaultCulture) ?? CultureInfo.InvariantCulture;
        ApplyToThreads();
    }

    public string this[string key]
    {
        get
        {
            if (_config.LabelOverrides.TryGetValue($"{Culture.TwoLetterISOLanguageName}:{key}", out var cultureOverride))
                return cultureOverride;
            if (_config.LabelOverrides.TryGetValue(key, out var generalOverride))
                return generalOverride;
            return Resources.GetString(key, Culture) ?? key;
        }
    }

    public FlowDirection FlowDirection =>
        Culture.TextInfo.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    public void SetCulture(string cultureName)
    {
        var culture = SafeCulture(cultureName);
        if (culture is null || culture.Name == Culture.Name)
            return;

        Culture = culture;
        _preferences.Set(CulturePreferenceKey, culture.Name);
        ApplyToThreads();
        // "Item" invalidates all indexer bindings at once.
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FlowDirection)));
    }

    /// <summary>Switch to the next culture in ShopConfig.SupportedCultures.</summary>
    public void CycleCulture()
    {
        var cultures = _config.SupportedCultures;
        if (cultures.Count < 2)
            return;
        var index = 0;
        for (var i = 0; i < cultures.Count; i++)
        {
            if (SafeCulture(cultures[i])?.Name == Culture.Name)
            {
                index = i;
                break;
            }
        }
        SetCulture(cultures[(index + 1) % cultures.Count]);
    }

    private void ApplyToThreads()
    {
        CultureInfo.DefaultThreadCurrentCulture = Culture;
        CultureInfo.DefaultThreadCurrentUICulture = Culture;
        CultureInfo.CurrentCulture = Culture;
        CultureInfo.CurrentUICulture = Culture;
    }

    private static CultureInfo? SafeCulture(string name)
    {
        try
        {
            return CultureInfo.GetCultureInfo(name);
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }
}
