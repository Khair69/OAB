# 10 — Implementation Status

[← 09 Design Decisions](09-decisions.md) · [Index](README.md)

---

An honest inventory of what exists, as of the correction-flow change
(2026-07-22). This is the document to read before deciding what to build next;
the schedule for doing so is [`ROADMAP.md`](ROADMAP.md).

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
| **Correction flow — fix a wrong entry without editing history** | ✅ Complete | `PartyStatementViewModel.CorrectAsync`, `LedgerMath.CorrectionDelta` |
| Backup: `.db` snapshot via share sheet | ✅ Complete | `Oab.Modules.Backup` |
| Backup: human-readable text summary | ✅ Complete | `LedgerSummaryReport` |
| Restore with validation + `.pre-restore` safety copy | ✅ Complete | `DatabaseBackupService` |
| Arabic + English, live switching, RTL | ✅ Complete | `LocalizationManager` |
| Arabic-Indic digit **output** | ✅ Complete | `MoneyFormat` |
| Per-shop wording via `LabelOverrides` | ✅ Complete | `ShopConfig` |
| Automatic schema migration on upgrade | ✅ Implemented, ❗untested with real data | `OabApp` |
| Module system + per-shop composition | ✅ Complete (one head exists) | `IOabModule`, `UseOab` |
| Arabic-Indic digit **input** | ❌ Missing | see §4 |
| Global exception handling | ⚠️ Backup and statement pages only | see §4 |
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

The correction flow adds no screen — it is an action sheet and two prompts on the
statement page ([04 §9](04-app-shell.md#the-correction-flow)).

## 3. Engine capability vs UI exposure

The engine is ahead of the app. These are all **implemented and tested in
`Oab.Core` / `Oab.Data`** but have no screen calling them — the cheapest
features to ship, because the hard half is already done and covered by tests.

| Capability | Implemented in | Called by the app? |
|---|---|---|
| `LedgerService.RecordSaleAsync(paidNow: true)` — a cash sale | `LedgerService.cs` | ❌ only the credit path is used (`CustomersViewModel.RecordDebtAsync`) |
| `ILedgerStore.UpdatePartyAsync` — rename, add a phone, archive | `LedgerStore.cs:21` | ❌ no caller |
| `Party.Phone`, `Party.Note` | persisted | ❌ never set or displayed |
| `Party.IsArchived` — hide a settled party without deleting history | persisted, honoured by every query | ❌ no screen sets it |
| `Document.Number` — the supplier's invoice number | persisted | ❌ never set or displayed |
| `DocumentLine` — item, quantity, unit price | persisted, `Total` computed | ❌ no entry UI; `RecordPurchaseAsync` accepts lines and nothing passes any |
| `ILedgerStore.GetDocumentAsync` (single, with lines) | `LedgerStore.cs` | ❌ no caller |
| `ILedgerStore.GetDocumentsAsync(partyId:)` filter | `LedgerStore.cs` | ❌ only the `kind` filter is used |
| Resource keys `Common_Unpaid`, `Common_Add`, `Party_Name`, `Party_Phone` | both `.resx` files | ❌ unused |

`RecordAdjustmentAsync` used to head this list. It now has a caller
(`PartyStatementViewModel.CorrectAsync`), which is why the most damaging entry in
§4 is gone. The rest are still the cheapest features to ship, because the hard
half is already done and covered by tests.

## 4. Known gaps and risks

Ordered roughly by how much damage each can do.

### ✅ Closed — a mistake could not be fixed

Tap an entry on the party statement → "Correct this entry" → what it should have
been → why. `LedgerMath.CorrectionDelta` turns the magnitude into a signed
adjustment; `RecordAdjustmentAsync` appends it with the corrected entry's
`DocumentId`, so the invoice's outstanding follows. Nothing is edited or deleted.
Full description in [04 §9](04-app-shell.md#the-correction-flow), reasoning in
D19–D20.

Not yet verified on a real phone in Arabic — like everything else here.

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

MAUI event handlers must be `async void`. `BackupPage` and now
`PartyStatementPage` funnel theirs through a local try/catch that turns a failure
into a message; **no other page does.** An exception from
`SuppliersPage.OnRecordPaymentClicked`, `CustomersPage.OnRecordDebtClicked`,
`PurchasesListPage.OnNewPurchaseClicked`, or their `OnAppearing` overrides
terminates the process with no message and no log.

Two pages having their own private copy of `RunAsync` is itself the signal that
this wants to be shared.

*Fix:* one `RunAsync` helper on a shared page base or extension, applied
everywhere, plus a global handler on `AppDomain.UnhandledException` /
`TaskScheduler.UnobservedTaskException` writing to a shareable log file. This is
the next roadmap item.

### 🟠 Arabic-Indic digits can be displayed but not typed

`ShopConfig.UseArabicIndicDigits` renders `٥٠`.
`NewPurchaseViewModel.TryParseAmount` (`:78`) tries `CurrentCulture` then
`InvariantCulture`; neither parses `٥٠`. The prompt-based parsers in
`SuppliersPage`, `CustomersPage`, and now `PartyStatementPage` (the correction
amount) have the same limitation — **four call sites, one missing function.**

*Fix:* map Arabic-Indic digits to ASCII before parsing — the inverse of
`MoneyFormat.MapDigits`, and it belongs next to it in Core so it is tested there.
Replace all four call sites in the same change. Until then, do not enable the
option for a shop.

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
- [x] Every mistake is fixable without rewriting history
- [ ] Works with zero internet, indefinitely
- [ ] Survives an app upgrade with real data
- [ ] Fully usable in Arabic on a cheap Android phone
- [ ] New shop → installed signed APK in under an hour
- [ ] Someone who has never seen it can log a purchase untaught

Two of eight. Both of the ones ticked are about a number being *explainable* and
*fixable* — the trust half of the product. Everything still open is about proving
it on real hardware in real hands.

## 7. Suggested order of work

Derived from the sections above, weighted by damage prevented per hour spent:

1. ~~**Correction flow**~~ — done.
2. **Global exception handler** — one shared helper, applied everywhere. Two
   pages now carry a private copy, which is the signal to hoist it.
3. **Arabic-Indic digit input** — a small pure function in Core, with tests, then
   four call sites.
4. **Run on a real Android phone in Arabic** — may invalidate assumptions. The
   correction flow's three stacked dialogs are the first thing to watch a real
   person get through.
5. **Restore onto a second device** — closes the existential risk.
6. **Upgrade test with real data.**
7. **Release engineering** — app id, icon, keystore, permissions.
8. Then the roadmap's Week 3–4 items: CashDay, tap-count reduction, the N+1 fix,
   scale testing, second shop, pilot.

---

[← Back to the documentation index](README.md)
