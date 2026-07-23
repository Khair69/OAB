# 10 — Implementation Status

[← 09 Design Decisions](09-decisions.md) · [Index](README.md)

---

An honest inventory of what exists, as of the store-coverage and N+1 change
(2026-07-23). This is the document to read before deciding what to build next;
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
| **Arabic-Indic digit input — every amount box, one parser** | ✅ Complete | `MoneyInput` |
| Per-shop wording via `LabelOverrides` | ✅ Complete | `ShopConfig` |
| Automatic schema migration on upgrade | ✅ Implemented, ❗untested with real data | `OabApp` |
| Module system + per-shop composition | ✅ Complete (one head exists) | `IOabModule`, `UseOab` |
| **Global exception handling — every handler funnelled, every crash logged** | ✅ Complete | `Oab.App/Diagnostics` |
| **Shareable error log** | ✅ Complete | `ErrorLog`, backup screen card |
| **Every `ILedgerStore` method run against real SQLite** | ✅ Complete | `tests/Oab.Data.Tests` |
| **Purchases list loads in a fixed number of queries** | ✅ Complete | `GetEntriesForDocumentsAsync` |
| **Zero-warning build** | ✅ Complete | D25 |
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
statement page ([04 §10](04-app-shell.md#the-correction-flow)). The error log
adds no screen either: it is a fourth card on the backup page, hidden until
something has actually gone wrong.

**Two of these six did not work.** Until the global exception handler was
installed, the purchases list and the party statement threw
`NotSupportedException` on every open — see §4.

## 3. Engine capability vs UI exposure

The engine is ahead of the app. These have no screen calling them — the cheapest
features to ship, because the hard half is already written.

> **This warning used to say "read tested narrowly".** `UpdatePartyAsync`,
> `GetDocumentAsync`, and `GetEntriesForDocumentAsync` were the three
> `ILedgerStore` methods with no test against real SQLite — the same description
> `GetDocumentsAsync` matched right up until it turned out to have thrown on
> every call it had ever received (§4).
>
> All three now have one, in `PartyEditingSqliteTests` and
> `DocumentLookupSqliteTests`, and **all three work.** Every method on
> `ILedgerStore` has now been run against a real database. The rows below are
> "no screen calls this", not "nobody knows if this works".
>
> One behaviour worth knowing before building on it: `UpdatePartyAsync` will
> **not** create a party — EF raises a concurrency failure instead of inserting.
> A party-editing screen needs `AddPartyAsync` for the new ones.

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
§4 is gone.

## 4. Known gaps and risks

Ordered roughly by how much damage each can do.

### ✅ Closed — a mistake could not be fixed

Tap an entry on the party statement → "Correct this entry" → what it should have
been → why. `LedgerMath.CorrectionDelta` turns the magnitude into a signed
adjustment; `RecordAdjustmentAsync` appends it with the corrected entry's
`DocumentId`, so the invoice's outstanding follows. Nothing is edited or deleted.
Full description in [04 §10](04-app-shell.md#the-correction-flow), reasoning in
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

Slightly better than it was: the global handler is installed **before**
`UseMauiApp`, so a migration crash now writes to `errors.log` and the next launch
can say what happened. It is still a crash — there is no recovery UI, and adding
one is a separate piece of work.

### ✅ Closed — unhandled exceptions crashed the app silently

Two layers now ([04 §9](04-app-shell.md#9-diagnostics--making-a-crash-leave-evidence),
reasoning in D21):

1. **`Page.RunSafelyAsync`** — one extension method, applied to *every*
   `async void` handler and `OnAppearing` override in the app and in all four
   modules. It logs the failure with the name of the handler it escaped from and
   turns it into a message. The two private `RunAsync` copies are gone;
   `BackupPage` keeps only its busy guard and its status-label reporting.
2. **`GlobalExceptionHandler`** — `AppDomain.UnhandledException`,
   `TaskScheduler.UnobservedTaskException`, `AndroidEnvironment.UnhandledExceptionRaiser`,
   and WinUI's handler, installed in `UseOab` **before `UseMauiApp`**, so the
   `Database.Migrate()` call in the `OabApp` constructor is covered too.

Everything lands in `FileSystem.AppDataDirectory/errors.log`, capped at 64 KB,
and the backup screen can share it. `NewPurchaseViewModel.SaveAsync` — a
`[RelayCommand]`, not an event handler, and therefore never covered by the page
funnel — is guarded too.

The handler **records; it does not recover.** Nothing marks an exception handled
or keeps the process alive: a ledger carrying on in an unknown state can write a
wrong number, which is worse than closing.

### ✅ Closed — two screens threw on every open

Found by the above, 25 seconds after it was first run.

`LedgerStore.GetDocumentsAsync` and `GetEntriesForPartyAsync` ordered by
`OccurredAt` **in SQL**. SQLite has no `DateTimeOffset`, and EF Core rejects the
query at translation time rather than falling back to client evaluation:

```
System.NotSupportedException: SQLite does not support expressions of type
'DateTimeOffset' in ORDER BY clauses.
   at Oab.Data.LedgerStore.GetDocumentsAsync(...) LedgerStore.cs:line 66
   at Oab.Modules.Purchases.PurchasesListViewModel.LoadAsync() ...
```

So **the purchases list and the party statement failed every single time they
were opened**, and this document previously listed both as ✅ Complete.

*Why nothing caught it:* neither method had a test against real SQLite, and the
41 view-model tests use `InMemoryLedgerStore`, which runs LINQ-to-Objects and
cannot reproduce a translation failure. The rule that follows — **a store method
with no real-SQLite test has never actually run** — is now in
[05 §6](05-modules.md#6-writing-a-new-module).

*Fixed* by ordering in C# after a server-side `WHERE`
([03 §4](03-data-layer.md#client-side-evaluation-and-why), D22), pinned by
`Documents_ComeBackNewestFirst`, `PartyEntries_ComeBackNewestFirst`, and
`OccurredAt_KeepsItsUtcOffset_AcrossStorage`.

*What it says about the rest of this document:* every ✅ above means "the code
exists and its tests pass", not "a person has seen it work". The two remaining
🔴 items are exactly the ones that need a person.

### ✅ Closed — Arabic-Indic digits could be displayed but not typed

`ShopConfig.UseArabicIndicDigits` rendered `٥٠`, and nothing in the app could
read `٥٠` back. Four screens each carried a private *try `CurrentCulture`, then
`InvariantCulture`* — one missing function with four call sites.

Closed by [`MoneyInput.TryParseAmount`](../src/Oab.Core/Formatting/MoneyInput.cs)
in Core, beside `MoneyFormat`, with all four call sites delegating to it and the
four copies deleted. It reads Arabic-Indic digits, the extended (Persian) forms
some Android keyboards offer, `٫` `٬` `،`, and the invisible bidi marks an RTL
entry field can wrap around the text. 35 tests
([`MoneyInputTests`](../tests/Oab.Core.Tests/MoneyInputTests.cs)), reasoning in
D23, the parser's table in
[02 §6](02-money-engine.md#the-inverse--moneyinputcs).

**What it turned up on the way.** .NET's `ar` uses `٫` as its decimal separator
and `٬` for grouping, so under Arabic the app was *already* printing separators
that a plain ASCII parser would reject — even with `UseArabicIndicDigits` off.
Fully configured, 1250.50 renders as `١٬٢٥٠٫٥٠`, containing no ASCII character at
all. That string is pre-filled into the correction flow's note prompt, so the one
screen for fixing wrong numbers was the one most likely to be handed a number it
could not read. Pinned by `TheShippingConfiguration_RoundTrips`.

Verified on Windows only, like everything else — but this is a pure function over
strings, so a phone can only change *what the keyboard emits*, not what the
parser does with it. If a real device produces a character not in the table, the
fix is one `case`.

### 🟠 Nothing has been verified on real Android hardware in Arabic

Everything so far is Windows-verified. Unproven on a cheap Android phone: RTL
layout under `ar`, Arabic font rendering, the `DatePicker` under `ar`, the
numeric keyboard, and the decimal separator. Any of these could be a
show-stopper and none of them will surface on a desktop.

The `ORDER BY DateTimeOffset` bug is the argument for taking this seriously:
"Windows-verified" turned out to include two screens that had never successfully
loaded. When the phone test happens, **read `errors.log` afterwards** — that is
now the difference between "it seemed fine" and knowing.

### ✅ Closed — N+1 query in the purchases list

`PurchasesListViewModel.LoadAsync` called `GetEntriesForDocumentAsync` **once per
document inside the loop** — one round trip per row, on every `OnAppearing`.
Fine at 10 purchases; 2,000 queries to draw one screen at two thousand.

Closed by `ILedgerStore.GetEntriesForDocumentsAsync`: one query for every
invoice on the page, grouped in memory, keyed by id — the shape
`GetBalancesAsync` already had. The screen is now three queries regardless of
how many rows it draws. Reasoning in D24, SQL in
[03 §4.2](03-data-layer.md#42-asking-about-many-invoices-at-once).

Worth knowing for the next such query: written the obvious way, EF sends **one
SQL parameter per id**, which is a ceiling waiting for a shop to grow into it.
`EF.Parameter` makes it one JSON parameter instead. This was found by reading the
generated SQL — the method's first comment confidently described the opposite.

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

### ✅ Closed — the build printed three permanent warnings

`EF1002` on `Oab.Data`, `CS8602` on `Oab.Modules.Purchases`, and `MA002` on
`Oab.App.Tests`. All understood, all harmless, all printed on every build —
which is the problem. `dotnet build OAB.slnx` now reports **0 warnings, 0
errors**, so a new one is visible without anyone having to remember which three
were expected. `CS8602` was restructured away rather than suppressed; `MA002`
was fixed by referencing `Microsoft.Maui.Controls` explicitly; only `EF1002` is
suppressed, at one statement, with the reasoning inline. D25.

### 🟢 Minor

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
2. ~~**Global exception handler**~~ — done, and it immediately found a
   `NotSupportedException` on two screens.
3. ~~**Arabic-Indic digit input**~~ — done: `MoneyInput` in Core, 35 tests, four
   call sites replaced and their private copies deleted.
4. ~~**Real-SQLite tests for the three untested store methods**~~ — done, along
   with the N+1 fix in the same area and the last three build warnings. All three
   methods work; see §3.
5. **Run on a real Android phone in Arabic** — may invalidate assumptions. The
   correction flow's three stacked dialogs are the first thing to watch a real
   person get through. **Read `errors.log` afterwards**, whether or not anything
   looked wrong.
6. **Restore onto a second device** — closes the existential risk.
7. **Upgrade test with real data.**
8. **Release engineering** — app id, icon, keystore, permissions.
9. Then the roadmap's remaining Week 3–4 items: CashDay, tap-count reduction,
   scale testing, second shop, pilot.

Steps 5–8 all need a physical phone or a signing key. **There is nothing left in
this list that can be finished at a desk** — which is itself the finding, and the
argument for stopping code work until the app has been on hardware.

**A standing item, promoted by what step 2 found:** any `ILedgerStore` method
without a test against real SQLite should be treated as untested code, not as
working code. As of step 4 there are none — every method has been run against a
real database. The item stays because the *next* method added is what it is for.

**A second standing item, from step 3:** the same logic copied into four screens
is untested by construction — nobody writes a test for a private method in a
page's code-behind. When a rule shows up twice, it belongs in Core, where the
tests are cheap. Steps 2 and 3 both closed bugs whose real cause was *where the
code lived*, not what it said. Step 4 applied the same rule to test scaffolding:
three copies of the SQLite harness became one `SqliteTestDatabase`, on the
grounds that duplicated setup rots exactly the way duplicated logic does.

**A third, from step 4:** *assumed* behaviour and *observed* behaviour are as far
apart as untested and tested. The batched query shipped with a comment asserting
something EF Core does not do; the fix was to read the generated SQL, which took
two minutes. When a line's correctness depends on what a framework emits, look at
what it emits.

---

[← Back to the documentation index](README.md)
