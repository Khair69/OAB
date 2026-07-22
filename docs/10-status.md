# 10 — Implementation Status

[← 09 Design Decisions](09-decisions.md) · [Index](README.md)

---

An honest inventory of what exists, as of commit `5ef15a9` (2026-07-22). This is
the document to read before deciding what to build next; the schedule for doing
so is [`ROADMAP.md`](ROADMAP.md).

---

## 1. Feature matrix

| Capability | Status | Where |
|---|---|---|
| Append-only ledger with signed entries | ✅ Complete | `Oab.Core/Ledger` |
| Party balances, document outstanding | ✅ Complete | `LedgerMath`, `LedgerService` |
| Purchases: log on credit or cash | ✅ Complete | `Oab.Modules.Purchases` |
| Purchases: pay remaining on an invoice | ✅ Complete | `PurchasesListViewModel` |
| Suppliers: list, balances, add, record payment | ✅ Complete | `Oab.Modules.SupplierDebts` |
| Customers: list, balances, add, record debt, collect payment | ✅ Complete | `Oab.Modules.CustomerDebts` |
| Party statement with running balance | ✅ Complete | `Oab.App/Views` |
| Backup: `.db` snapshot via share sheet | ✅ Complete | `Oab.Modules.Backup` |
| Backup: human-readable text summary | ✅ Complete | `LedgerSummaryReport` |
| Restore with validation + `.pre-restore` safety copy | ✅ Complete | `DatabaseBackupService` |
| Arabic + English, live switching, RTL | ✅ Complete | `LocalizationManager` |
| Arabic-Indic digit **output** | ✅ Complete | `MoneyFormat` |
| Per-shop wording via `LabelOverrides` | ✅ Complete | `ShopConfig` |
| Automatic schema migration on upgrade | ✅ Implemented, ❗untested with real data | `OabApp` |
| Module system + per-shop composition | ✅ Complete (one head exists) | `IOabModule`, `UseOab` |
| **Correction / adjustment flow in the UI** | ❌ **Engine only — no caller** | see §3 |
| Arabic-Indic digit **input** | ❌ Missing | see §4 |
| Global exception handling | ❌ Only on the Backup page | see §4 |
| Editing a party (phone, note, archive) | ❌ No screen | see §3 |
| Document line items in the UI | ❌ Engine only | see §3 |
| Sales module (cash sales, receipts) | ❌ Not built | — |
| Inventory / stock quantities | ❌ Not built (deliberately deferred) | — |
| CashDay / today summary | ❌ Not built | roadmap Week 3 |
| Search, archiving of settled items | ❌ Not built | roadmap Week 3 |
| Weekly backup reminder | ❌ Not built | — |
| Multi-device sync, cloud | ❌ **Out of v1 on purpose** | — |
| Receipt printing, barcode scanning | ❌ **Out of v1 on purpose** | — |
| iOS / macOS | ❌ Scaffolding present, not targeted | [08 §7](08-build-test-release.md#7-platform-targets) |
| Signed release APK | ❌ Not set up | [08 §8](08-build-test-release.md#8-release--what-is-missing) |

## 2. Screen inventory

Six screens exist. That is the whole product surface today.

| Screen | File | Reachable from |
|---|---|---|
| Purchases list | `PurchasesListPage.xaml` | Flyout |
| New purchase | `NewPurchasePage.xaml` | Purchases list `＋` |
| Suppliers | `SuppliersPage.xaml` | Flyout |
| Customers | `CustomersPage.xaml` | Flyout |
| Backup | `BackupPage.xaml` | Flyout |
| Party statement | `PartyStatementPage.xaml` | Tapping a supplier or customer card |

## 3. Engine capability vs UI exposure

The engine is ahead of the app. These are all **implemented and tested in
`Oab.Core` / `Oab.Data`** but have no screen calling them — the cheapest
features to ship, because the hard half is already done and covered by tests.

| Capability | Implemented in | Called by the app? |
|---|---|---|
| `LedgerService.RecordAdjustmentAsync` — the correction mechanism | `LedgerService.cs:141` | ❌ **tests only** |
| `LedgerService.RecordSaleAsync(paidNow: true)` — a cash sale | `LedgerService.cs` | ❌ only the credit path is used (`CustomersViewModel.RecordDebtAsync`) |
| `ILedgerStore.UpdatePartyAsync` — rename, add a phone, archive | `LedgerStore.cs:21` | ❌ no caller |
| `Party.Phone`, `Party.Note` | persisted | ❌ never set or displayed |
| `Party.IsArchived` — hide a settled party without deleting history | persisted, honoured by every query | ❌ no screen sets it |
| `Document.Number` — the supplier's invoice number | persisted | ❌ never set or displayed |
| `DocumentLine` — item, quantity, unit price | persisted, `Total` computed | ❌ no entry UI; `RecordPurchaseAsync` accepts lines and nothing passes any |
| `ILedgerStore.GetDocumentAsync` (single, with lines) | `LedgerStore.cs` | ❌ no caller |
| `ILedgerStore.GetDocumentsAsync(partyId:)` filter | `LedgerStore.cs` | ❌ only the `kind` filter is used |
| Resource keys `Common_Unpaid`, `Common_Add`, `Party_Name`, `Party_Phone` | both `.resx` files | ❌ unused |

The most valuable of these is the first. **A typo'd amount is permanent today.**

## 4. Known gaps and risks

Ordered roughly by how much damage each can do.

### 🔴 A mistake cannot be fixed

`RecordAdjustmentAsync` exists, is tested, renders correctly on the statement
page (labelled "Correction", gold-outlined, note shown) — and **no screen calls
it**. A shopkeeper who types 1000 instead of 100 has no recourse inside the app.

*Fix:* long-press an entry on the statement page → "correct this" → prompt for a
signed amount and a mandatory note → `RecordAdjustmentAsync`. The statement page
already renders the result; this is a code-behind handler and a prompt.

### 🔴 Data loss is not yet proven survivable

Backup and restore are implemented and covered by six tests against real SQLite
files. But **restoring onto a different physical phone has never been done.**
Until it has, the product's central safety promise is a claim, not a fact.

Related: there is **no weekly backup reminder**. A backup feature nobody
remembers to use protects nobody.

### 🔴 The upgrade path is untested against real data

`Database.Migrate()` runs at startup (D10) and a migration failure is a startup
crash with no recovery UI. The sequence *install v1 → enter real data → install
v2 carrying a new migration → confirm nothing is lost* has not been performed.

### 🟠 Unhandled exceptions crash the app silently

MAUI event handlers must be `async void`. `BackupPage` funnels all of its through
a try/catch that turns a failure into a status message; **no other page does.**
An exception from `SuppliersPage.OnRecordPaymentClicked`,
`CustomersPage.OnRecordDebtClicked`, `PurchasesListPage.OnNewPurchaseClicked`, or
any `OnAppearing` override terminates the process with no message and no log.

*Fix:* a shared `RunAsync` helper (copy `BackupPage`'s), plus a global handler on
`AppDomain.UnhandledException` / `TaskScheduler.UnobservedTaskException` writing
to a shareable log file.

### 🟠 Arabic-Indic digits can be displayed but not typed

`ShopConfig.UseArabicIndicDigits` renders `٥٠`.
`NewPurchaseViewModel.TryParseAmount` (`:78`) tries `CurrentCulture` then
`InvariantCulture`; neither parses `٥٠`. The two prompt-based parsers in
`SuppliersPage` and `CustomersPage` have the same limitation.

*Fix:* map Arabic-Indic digits to ASCII before parsing — the inverse of
`MoneyFormat.MapDigits`, and it belongs next to it in Core so it is tested there.
Until then, do not enable the option for a shop.

### 🟠 Nothing has been verified on real Android hardware in Arabic

Everything so far is Windows-verified. Unproven on a cheap Android phone: RTL
layout under `ar`, Arabic font rendering, the `DatePicker` under `ar`, the
numeric keyboard, and the decimal separator. Any of these could be a
show-stopper and none of them will surface on a desktop.

### 🟡 N+1 query in the purchases list

`PurchasesListViewModel.LoadAsync` (`:48`) calls `GetEntriesForDocumentAsync`
**once per document inside the loop**. Fine at 10 purchases; painful at 2,000, on
a low-end phone, on every `OnAppearing`.

*Fix:* one query for all entries whose `DocumentId` is in the page's set, grouped
in memory — the same shape `GetBalancesAsync` already uses.

### 🟡 `GetBalancesAsync` reads the entire ledger

Every call projects `(PartyId, Amount)` for **all** rows and groups in C#
(unavoidable given decimal-as-TEXT, D6). Called on every load of the supplier
and customer lists. At a few thousand entries this is still milliseconds; it is
the first thing to profile after seeding realistic data.

### 🟡 Lists have no search, no paging, no archiving

Every list loads everything. A shop two years in will scroll past hundreds of
settled parties to find the one they want. `Party.IsArchived` is already
implemented and honoured by every query — it just needs a screen.

### 🟡 Release engineering is not started

Debug-signed APKs, `com.companyname.*` application id, .NET template icon and
splash, no version scheme, and `INTERNET` + `ACCESS_NETWORK_STATE` permissions
still declared in `AndroidManifest.xml` for an app that makes no network calls.
Full list: [08 §8](08-build-test-release.md#8-release--what-is-missing).

### 🟢 Minor

- **`EF1002` warning** on every build of `Oab.Data`
  ([`DatabaseBackupService.cs:29`](../src/Oab.Data/Backup/DatabaseBackupService.cs)).
  Expected and safe — `VACUUM INTO` cannot be parameterised and the path is
  escaped — but it should be suppressed with a `#pragma` and a comment so it
  stops being noise that trains people to ignore warnings.
- **Dead scaffolding** in the customer template: `Platforms/iOS/`,
  `Platforms/MacCatalyst/`, and `Resources/Images/dotnet_bot.png` are all
  committed but unused. (`.vs/` and `*.csproj.user` exist on disk but are
  correctly gitignored.)
- **`IOabModule.RegisterRoutes()`** has no implementation in any module. It is a
  reasonable extension point; it is currently unexercised.
- **Four unused resource keys** ([06 §4](06-localization.md#4-resource-key-catalogue)).
- **`LocalizationManager` has no direct unit tests.** It is exercised indirectly
  by every view-model test, but the override-precedence chain, `CycleCulture`
  wrap-around, and `SafeCulture` fallback have no dedicated coverage.

## 5. What is deliberately absent

Not gaps. Cutting these **is** the product — the differentiator is being
*smaller* than a POS.

- Multi-device sync, cloud anything
- Receipt printing, barcode scanning
- Stock quantities and inventory valuation
- Reports beyond balances and the day view
- iOS
- User accounts, permissions, multi-user

Each is "later, if a paying shop asks."

## 6. Definition of "hole proof"

From [`ROADMAP.md`](ROADMAP.md), reproduced here because it is the real
acceptance criteria for v1:

- [ ] Data survives a lost phone — *proven by restoring onto a different device*
- [x] Every balance taps through to the entries that produced it
- [ ] Every mistake is fixable without rewriting history
- [ ] Works with zero internet, indefinitely
- [ ] Survives an app upgrade with real data
- [ ] Fully usable in Arabic on a cheap Android phone
- [ ] New shop → installed signed APK in under an hour
- [ ] Someone who has never seen it can log a purchase untaught

One of eight. The engine is in good shape; almost everything remaining is about
proving it on real hardware in real hands.

## 7. Suggested order of work

Derived from the sections above, weighted by damage prevented per hour spent:

1. **Correction flow** — the engine is done; this is a prompt and a handler.
2. **Global exception handler** — one shared helper, applied everywhere.
3. **Arabic-Indic digit input** — a small pure function in Core, with tests.
4. **Run on a real Android phone in Arabic** — may invalidate assumptions.
5. **Restore onto a second device** — closes the existential risk.
6. **Upgrade test with real data.**
7. **Release engineering** — app id, icon, keystore, permissions.
8. Then the roadmap's Week 3–4 items: CashDay, tap-count reduction, the N+1 fix,
   scale testing, second shop, pilot.

---

[← Back to the documentation index](README.md)
