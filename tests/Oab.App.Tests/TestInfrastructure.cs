using Microsoft.Maui.Storage;
using Oab.App;
using Oab.App.Formatting;
using Oab.App.Localization;
using Oab.Core.Ledger;
using Oab.TestSupport;

namespace Oab.App.Tests;

/// <summary>In-memory <see cref="IPreferences"/> so localization needs no device.</summary>
internal sealed class FakePreferences : IPreferences
{
    private readonly Dictionary<string, object?> _values = [];

    public bool ContainsKey(string key, string? sharedName = null) => _values.ContainsKey(key);
    public void Remove(string key, string? sharedName = null) => _values.Remove(key);
    public void Clear(string? sharedName = null) => _values.Clear();
    public void Set<T>(string key, T value, string? sharedName = null) => _values[key] = value;

    public T Get<T>(string key, T defaultValue, string? sharedName = null) =>
        _values.TryGetValue(key, out var v) && v is T typed ? typed : defaultValue;
}

/// <summary>
/// Assembles the real dependency graph a module view model expects — store,
/// ledger service, localization, money formatter — with fakes standing in only
/// for the database and device preferences. English culture keeps label
/// assertions deterministic.
/// </summary>
internal sealed class VmContext
{
    public InMemoryLedgerStore Store { get; } = new();
    public LedgerService Ledger { get; }
    public LocalizationManager Localization { get; }
    public IMoneyFormatter Money { get; }
    public ShopConfig Config { get; }

    public VmContext(string culture = "en")
    {
        Config = new ShopConfig
        {
            ShopName = "Test Shop",
            CurrencySymbol = "SP",
            DefaultCulture = culture,
            SupportedCultures = ["ar", "en"],
        };
        Ledger = new LedgerService(Store);
        Localization = new LocalizationManager(Config, new FakePreferences());
        Money = new MoneyFormatter(Config, Localization);
    }
}
