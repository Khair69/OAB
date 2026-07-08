\# OAB — Customizable Ledger/POS for Souk Shops



\## Context



Small shops in the souk (shampoo/makeup/clothing resellers who buy in bulk from distributors) track everything with pen and notebook. They skip existing POS systems not because of price but because of \*\*complexity\*\* — too many unused features, too much logging effort. The product idea: a minimal, per-shop-customized system whose core job is \*\*money tracking\*\* — what the shop owes suppliers, what customers owe the shop, and whether things are paid.



Decisions already made with the user:

\- \*\*Device:\*\* Android phone first (shopkeepers already own one)

\- \*\*Language:\*\* Arabic + one more language, RTL required from day one

\- \*\*Delivery:\*\* custom build per shop (but structured so builds are cheap — see below)

\- \*\*Data:\*\* single device, fully offline, local storage; sync/backup later



\## Tech Stack (recommendation)



| Layer | Choice | Why |

|---|---|---|

| UI | \*\*.NET MAUI\*\* (Android target; Windows target comes almost free later) | The C# path to Android. Built-in RTL (`FlowDirection`), `.resx` localization, one codebase if a desktop version is ever wanted. |

| MVVM | \*\*CommunityToolkit.Mvvm\*\* | Standard, low-boilerplate (`\[ObservableProperty]`, `\[RelayCommand]`). |

| Storage | \*\*SQLite + EF Core\*\* | Offline-first. EF Core migrations matter here: per-shop custom builds will evolve schemas independently, and migrations keep upgrades safe. |

| Core logic | \*\*Plain .NET class library\*\* (no UI/EF dependencies) | The money engine must be testable and reusable regardless of what UI each customer gets. |

| Tests | \*\*xUnit\*\* on the core library | Ledger math is the one thing that must never be wrong. |

| Sync (later) | ASP.NET Core Minimal API | Not in v1. The append-only ledger design below makes retrofitting sync easy. |



Alternative considered: Avalonia (better desktop, weaker mobile story) and Blazor Hybrid (good if you prefer HTML/CSS for UI skinning). MAUI + XAML is the safest Android-first C# choice.



\## The Customization Architecture (the important part)



"Custom build per shop" must NOT mean "fork the repo per shop" — that drowns you after \~5 customers. Instead: \*\*product-line architecture\*\*. One repo, shared core, and each customer is a tiny composition project.



```

OAB.sln

├── src/

│   ├── Oab.Core/            # Domain + money engine. Zero UI/DB dependencies.

│   ├── Oab.Data/            # EF Core, SQLite, migrations

│   ├── Oab.App/             # MAUI shared shell: navigation, theming, localization, module host

│   └── Oab.Modules/         # Optional feature modules (each self-contained)

│       ├── Oab.Modules.SupplierDebts/

│       ├── Oab.Modules.CustomerDebts/

│       ├── Oab.Modules.Purchases/

│       ├── Oab.Modules.Sales/

│       ├── Oab.Modules.Inventory/      # optional — many shops won't want item tracking

│       └── Oab.Modules.CashDay/        # daily cash in/out summary

├── customers/

│   ├── Oab.Customer.Template/   # copy this to onboard a new shop

│   └── Oab.Customer.<Shop>/     # per-shop: picks modules, shop config, custom module(s) if any

└── tests/

&#x20;   └── Oab.Core.Tests/

```



\*\*A module is:\*\* a class library with a `IModule` implementation that registers its services in DI, contributes its pages + menu items to the shell, and declares its EF entity configurations. The shell (`Oab.App`) discovers modules from whatever the customer project references.



\*\*A customer project is:\*\* a MAUI head project (\~50 lines) that references `Oab.App` + the modules that shop wants, plus a `ShopConfig` (shop name, currency, language default, field labels, feature toggles inside modules). A shop that "only wants to log what they buy and whether it's paid" = Template + `Purchases` + `SupplierDebts`. Nothing else appears in their UI.



\*\*When a shop wants something truly custom\*\* ("something something"): write it as a new module in \*their\* customer folder. If a second shop later wants it, promote it to `Oab.Modules/`. Custom code never touches core.



\## The Money Engine (Oab.Core)



Model money as an \*\*append-only ledger\*\*, not as mutable balance fields. This is the single design decision that makes everything else (debts, partial payments, history, future sync, "undo") fall out for free.



\- \*\*Party\*\* — one entity for suppliers, customers, or both (souk reality: the same person can be either). Has a derived balance, never a stored one.

\- \*\*LedgerEntry\*\* — immutable: `(Id, PartyId, Timestamp, Amount signed, Kind, Note, DocumentId?)`. Kinds: `PurchaseOnCredit`, `PurchaseCash`, `Sale`, `SaleOnCredit`, `PaymentOut`, `PaymentIn`, `Adjustment`.

\- \*\*Document\*\* — optional grouping (a purchase invoice with line items) that \*posts\* ledger entries. Line items/products only exist if the Inventory/Purchases modules are on; the ledger works at pure money level without them.

\- \*\*Balances\*\* = `SUM(entries)` per party. "What do I owe the distributor?" and "what does customer X owe me?" are the same query with opposite sign.

\- Corrections are \*\*new adjustment entries\*\*, never edits — matches how a paper notebook actually works, keeps history honest, and makes future device-sync trivial (append-only merges cleanly).

\- Money as `decimal`, currency configured per shop. All timestamps UTC + stored local offset.



\## Localization / RTL



\- `.resx` per language (`ar` default, second language switchable in settings), `FlowDirection` bound to culture from the first screen — never retrofitted.

\- Per-shop \*\*label overrides\*\* in `ShopConfig` (one shop calls it "دفتر", another "حساب") so wording customization needs no code.

\- Numerals: Western digits by default, Arabic-Indic as a config toggle.



\## Build Order



1\. \*\*Scaffold\*\* — solution layout above; `dotnet new` projects, wire DI + module host skeleton in `Oab.App`.

2\. \*\*Core money engine\*\* — `Party`, `LedgerEntry`, posting rules, balance queries, in `Oab.Core` with xUnit tests. Pure C#, no UI. \*Do this before any screens.\*

3\. \*\*Data layer\*\* — EF Core SQLite mapping + initial migration; repository/service layer used by modules.

4\. \*\*App shell\*\* — MAUI navigation, localization + RTL, theming, module discovery, `ShopConfig` loading.

5\. \*\*First two modules\*\* — `SupplierDebts` + `Purchases` (log a purchase, mark paid/unpaid, see per-supplier balance). This alone is a sellable v1 for the notebook-and-pen use case.

6\. \*\*Remaining base modules\*\* — `CustomerDebts`, `PaymentIn/Out` screens, `CashDay` summary.

7\. \*\*Backup/export\*\* — share the SQLite file (WhatsApp/Drive) + a plain-text summary export. Cheap insurance long before real sync.

8\. \*\*Pilot customer\*\* — copy `Oab.Customer.Template`, configure for one real shop, iterate on the ground.



\## Verification



\- `dotnet test` on `Oab.Core.Tests` — ledger math: posting, partial payments, balance signs, adjustment/correction flows.

\- Run the Template customer app on an Android emulator (`dotnet build -t:Run -f net9.0-android`): switch language to Arabic, confirm RTL layout, log a purchase on credit, make a partial payment, verify the supplier balance.

\- Kill and reopen the app mid-flow to confirm everything persisted to SQLite (offline durability is the core promise).



\## Explicitly Out of v1



Multi-device sync, cloud anything, receipt printing, barcode scanning, reports beyond simple balances/day summary. The pitch is \*less\* than a POS, not more.

