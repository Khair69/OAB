# 04 — App Shell (`Oab.App`)

[← 03 Data Layer](03-data-layer.md) · [Index](README.md) · Next: [05 — Feature Modules](05-modules.md)

---

`Oab.App` is the shared MAUI application: the module host, the flyout chrome,
localization, per-shop configuration, money formatting, and the one detail
screen every shop gets regardless of which modules they bought.

**It contains no features.** Search it for "Purchases" or "Supplier" and you
find nothing. That is the point.

```
src/Oab.App/
├── OabApp.cs                     Application subclass — migrate, create window
├── OabAppBuilderExtensions.cs    UseOab(...) — the single entry point
├── OabShell.cs                   Flyout built from module nav items
├── OabServices.cs                Static service-locator escape hatch
├── ShopConfig.cs                 Everything about one shop that is configuration
├── Modules/IOabModule.cs         The module contract + OabNavItem
├── Localization/
│   ├── LocalizationManager.cs    Lookup, live switching, persistence, FlowDirection
│   └── TrExtension.cs            {oab:Tr Key} XAML markup extension
├── Formatting/MoneyFormatter.cs  IMoneyFormatter — Core's MoneyFormat bound to this shop
├── Resources/Strings.resx        English
├── Resources/Strings.ar.resx     Arabic
└── Views/                        PartyStatementPage + ViewModel + StatementRow
```

Targets `net10.0-android` and `net10.0-windows10.0.19041.0`; minimum Android API
21, minimum Windows 10.0.17763.0. XAML is compiled via source generation
(`<MauiXamlInflator>SourceGen</MauiXamlInflator>`).

---

## 1. `UseOab` — the composition root

[`OabAppBuilderExtensions.cs`](../src/Oab.App/OabAppBuilderExtensions.cs)

```csharp
public static MauiAppBuilder UseOab(
    this MauiAppBuilder builder, ShopConfig config, params IOabModule[] modules)
```

This one method is the entire public API a customer head project needs. It:

1. calls `builder.UseMauiApp<OabApp>()`;
2. registers `ShopConfig`, `LocalizationManager` (and assigns the static
   `LocalizationManager.Current` inside the factory), `IMoneyFormatter`;
3. calls `AddOabData(Path.Combine(FileSystem.AppDataDirectory, config.DatabaseFileName))`;
4. registers the module list itself and the `OabShell`;
5. registers `PartyStatementViewModel` + `PartyStatementPage` — the shared detail
   pages every shop gets, module list notwithstanding;
6. for each module: `ConfigureServices(services)`, `RegisterRoutes()`, and
   `AddTransient(navItem.PageType)` for every nav item it declares.

**Module order is menu order.**

## 2. `OabApp` — [`OabApp.cs`](../src/Oab.App/OabApp.cs)

A tiny `Application` subclass whose constructor does two consequential things
before any page can exist:

```csharp
OabServices.Provider = services;      // enable the locator escape hatch
using var db = dbFactory.CreateDbContext();
db.Database.Migrate();                // bring the shop's file to the current schema
```

Migrating in the constructor means **upgrades are automatic**: install a new APK
over an old one and the database is current before the first screen loads. It is
a local SQLite file, so this is fast even on an old phone.

`CreateWindow` resolves the `OabShell` and titles the window with the shop name.

## 3. `OabShell` — [`OabShell.cs`](../src/Oab.App/OabShell.cs)

The whole app chrome, built at runtime:

- `FlyoutBehavior.Flyout`.
- `FlowDirection` is **bound** to `LocalizationManager.FlowDirection`, not set.
  RTL therefore flips live when the language changes, with no restart. This
  binding is established on the very first screen, which is the only way RTL
  ever works properly — it cannot be retrofitted.
- `FlyoutHeader` is a bold 24pt label showing `config.ShopName`.
- For each nav item of each module: a `FlyoutItem` containing a `ShellContent`
  whose `ContentTemplate` resolves the page from DI on demand. Both the flyout
  item's and the content's `Title` are **bound** to
  `localization[$"[{nav.TitleKey}]"]`, so menu labels re-render on a language
  switch.
- `FlyoutFooter` is a button bound to `Common_Language`; clicking it calls
  `localization.CycleCulture()`.

That is the entire shell. Roughly 45 lines, and it has no idea what a purchase
is.

## 4. `IOabModule` — [`Modules/IOabModule.cs`](../src/Oab.App/Modules/IOabModule.cs)

```csharp
public interface IOabModule
{
    string Name { get; }                                  // stable id, for logs/diagnostics
    void ConfigureServices(IServiceCollection services);  // pages, view models, services
    IEnumerable<OabNavItem> GetNavItems();                // top-level flyout entries
    void RegisterRoutes() { }                             // optional; default no-op
}

public record OabNavItem(string TitleKey, string Route, Type PageType);
```

`TitleKey` is a **resource key**, never display text — that is what allows a shop
to rename "Purchases" to "الدفتر" through `LabelOverrides` without code.

See [05 — Feature Modules §6](05-modules.md#6-writing-a-new-module) for the
checklist to implement one.

## 5. `ShopConfig` — [`ShopConfig.cs`](../src/Oab.App/ShopConfig.cs)

Everything about one shop that should be configuration, not code. Full reference
in [07 — Customization](07-customization.md); the shape is:

| Property | Type | Default | Meaning |
|---|---|---|---|
| `ShopName` | `required string` | — | Window title and flyout header; also the backup filename stem |
| `CurrencySymbol` | `string` | `""` | Appended after amounts. Empty = numbers only |
| `DefaultCulture` | `string` | `"ar"` | Culture on first launch |
| `SupportedCultures` | `IReadOnlyList<string>` | `["ar","en"]` | What the language button cycles through |
| `UseArabicIndicDigits` | `bool` | `false` | Render `٠١٢٣` instead of `0123` |
| `LabelOverrides` | `IReadOnlyDictionary<string,string>` | empty | Per-shop wording, optionally culture-scoped |
| `DatabaseFileName` | `string` | `"oab.db"` | File name inside the app's private data directory |

## 6. Localization

### `LocalizationManager` — [`LocalizationManager.cs`](../src/Oab.App/Localization/LocalizationManager.cs)

Single source of truth for display text. Implements `INotifyPropertyChanged`
and exposes a **string indexer**, which is what makes live language switching
work.

**Lookup order** for `localization["Purchases_Title"]`:

1. `LabelOverrides["<lang>:Purchases_Title"]` — e.g. `"ar:Purchases_Title"`;
2. `LabelOverrides["Purchases_Title"]` — culture-independent override;
3. `Strings.resx` for the current culture, via `ResourceManager`;
4. **the key itself** — a missing key shows as `Purchases_Title` on screen, which
   is ugly and immediately visible, rather than blank.

**Live switching.** `SetCulture` persists the choice, re-applies the culture to
the thread defaults, then raises `PropertyChanged` for `"Item"` — the magic
property name that invalidates *every* indexer binding at once — plus
`FlowDirection`. Every `{oab:Tr …}` binding in the app re-resolves. No restart,
no page reload.

**Persistence.** The chosen culture is stored in `IPreferences` under the key
`oab.culture`. `IPreferences` is **injected**, not taken from `Preferences.Default`
inside the class, which is precisely why `LocalizationManager` — and every view
model that depends on it — can be unit-tested without a device
(`FakePreferences` in [`TestInfrastructure.cs`](../tests/Oab.App.Tests/TestInfrastructure.cs)).

**`FlowDirection`** is derived from `Culture.TextInfo.IsRightToLeft`.

**`CycleCulture`** finds the current culture's index in
`ShopConfig.SupportedCultures` and moves to the next, wrapping. If fewer than
two cultures are configured it does nothing — a single-language shop's button is
inert rather than broken.

**`SafeCulture`** wraps `CultureInfo.GetCultureInfo` in a try/catch for
`CultureNotFoundException`, with a fallback chain of *saved preference →
configured default → `InvariantCulture`*. A typo in a shop's config degrades to
English-ish formatting instead of crashing at startup.

### `TrExtension` — [`TrExtension.cs`](../src/Oab.App/Localization/TrExtension.cs)

```xml
xmlns:oab="clr-namespace:Oab.App.Localization;assembly=Oab.App"
...
<Label Text="{oab:Tr Purchases_Title}" />
```

Returns a `Binding` to `LocalizationManager.Current[Key]` in `OneWay` mode. It
produces a *live binding*, not a resolved string, which is the mechanism behind
instant language switching. `[ContentProperty(nameof(Key))]` is what allows the
terse `{oab:Tr SomeKey}` form.

> Inside `Oab.App` itself the namespace declaration drops the `;assembly=` part;
> in modules it must be included.

Full key catalogue: [06 — Localization & RTL](06-localization.md).

## 7. Money formatting — [`Formatting/MoneyFormatter.cs`](../src/Oab.App/Formatting/MoneyFormatter.cs)

```csharp
public interface IMoneyFormatter { string Format(decimal amount); }
```

A four-line adapter binding Core's pure `MoneyFormat.Format` to this shop's
`CurrencySymbol` and `UseArabicIndicDigits` and the current UI culture. All the
actual logic — and all of its tests — live in Core.

View models depend on `IMoneyFormatter`, never on `MoneyFormat` directly, so a
shop's currency and digit style are applied everywhere without a single call
site knowing about them.

## 8. `OabServices` — [`OabServices.cs`](../src/Oab.App/OabServices.cs)

```csharp
public static T Get<T>() where T : notnull;
```

A deliberate, documented service-locator escape hatch for the places MAUI does
not constructor-inject: pages pushed from code-behind, and prompt dialogs. It
throws a clear `InvalidOperationException` if used before `OabApp` starts.

Used in exactly two places today —
`PurchasesListPage.OnNewPurchaseClicked` and `PartyStatementPage.PushAsync`.

## 9. Party statement — shared detail screen

Files: [`Views/PartyStatementPage.xaml`](../src/Oab.App/Views/PartyStatementPage.xaml),
[`.xaml.cs`](../src/Oab.App/Views/PartyStatementPage.xaml.cs),
[`PartyStatementViewModel.cs`](../src/Oab.App/Views/PartyStatementViewModel.cs).

**Why it lives in the shell rather than in a module:** a party is often a
supplier and a customer at once, and both list screens push the same page. Put
it in `SupplierDebts` and `CustomerDebts` would have to reference it — breaking
the "modules never reference each other" rule.

> This is the screen that makes a balance *explainable*. A shopkeeper who cannot
> answer "why do I owe 500?" stops trusting the app within a week.

### How a list opens it

```csharp
await PartyStatementPage.PushAsync(Navigation, row.Party.Id, PartyRole.Supplier);
```

A static helper resolves the page from `OabServices`, stashes the party id and
the caller's perspective in private fields, and pushes it. Modules therefore need
to know nothing about how the page is built or how it receives its party.
`OnAppearing` calls `viewModel.LoadAsync(partyId, perspective)`.

### `StatementRow`

One entry, and the balance it left behind. All text is rendered in the view
model, so the XAML needs no converters:

| Member | Content |
|---|---|
| `Entry` | The underlying `LedgerEntry` |
| `DateText` | `OccurredAt` in `"d"` format, current culture |
| `KindText` | Localized kind label (see below) |
| `AmountText` | Magnitude via `IMoneyFormatter` — unsigned |
| `BalanceAfterText` | The running balance after this entry, **phrased in words** |
| `NoteText` / `HasNote` | The entry's note and whether to show it |
| `IsCorrection` | `Kind == Adjustment` |

### Ordering — the subtle part

```csharp
var chronological = entries.OrderBy(e => e.OccurredAt).ThenBy(e => e.CreatedAtUtc);
```

The store already returns entries newest-first, but the view model **re-sorts
ascending anyway**: a running balance only means anything in the order the money
actually moved, and `CreatedAtUtc` breaks ties between entries stamped with the
same `OccurredAt` (exactly what a cash purchase produces). The running total is
accumulated forward, then the rows are added to the `ObservableCollection` **in
reverse** — newest at the top, because the shopkeeper opened this screen to ask
"why is this number what it is?" and the answer is usually the last few lines,
not the ones from three months ago.

A backdated entry therefore lands in its correct chronological position with a
correct running balance, while the screen still reads newest-first. Pinned by
`PartyStatementViewModelTests.EntriesOutOfChronologicalOrderAreStillSummedInOrder`.

### Wording and colour

`Describe(balance)` phrases direction in words:

| Balance | Text |
|---|---|
| `< 0` | `Statement_YouOwe` + formatted magnitude |
| `> 0` | `Statement_TheyOwe` + formatted magnitude |
| `= 0` | `Statement_Settled` |

Colour is decided by the **perspective** — which list pushed the page. Red always
means "the debt this screen is about is still open", but suppliers and customers
point opposite ways, and this page is shared, so the caller says which way:

| Perspective | Balance `< 0` (shop owes) | Balance `> 0` (they owe) | `= 0` |
|---|---|---|---|
| `Supplier` | **Firebrick** | SeaGreen | Gray |
| `Customer` | SeaGreen | **Firebrick** | Gray |
| `None` or `Supplier \| Customer` | Gray | Gray | Gray |

A party who is both, or neither, has no expected direction and stays neutral.
The words carry the meaning on their own; the colour is only an accelerant. The
whole table is pinned by a `[Theory]` with seven cases.

`KindLabel` maps `EntryKind` to a resource key and falls back to
`kind.ToString()` for any kind added to Core before this screen learns about it —
an unknown kind shows something rather than an empty row.

### The page

`Grid` with a fixed header (party name, balance in the perspective colour) over a
`CollectionView`. Adjustment rows get a **goldenrod 1.5px outline** via a
`DataTrigger` on `IsCorrection` — a fixed accent that reads on both light and
dark themes. Corrections are outlined, not hidden: an edited-looking history is
the point. An `EmptyView` shows `Statement_Empty`.

## 10. Test coverage

[`tests/Oab.App.Tests/PartyStatementViewModelTests.cs`](../tests/Oab.App.Tests/PartyStatementViewModelTests.cs)
— 12 of the suite's 30 tests: running-balance accumulation, newest-first
ordering with the header matching the last balance, backdated entries, the
sale→payment-in path, corrections (labelled, flagged, note shown, original
untouched), an empty party, entries not leaking between parties, reload
idempotence, the seven-case colour matrix, settled never being alarming, and
Arabic labels.

---

Next: [05 — Feature Modules](05-modules.md)
