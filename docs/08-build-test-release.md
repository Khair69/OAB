# 08 — Build, Test & Release

[← 07 Customization](07-customization.md) · [Index](README.md) · Next: [09 — Design Decisions](09-decisions.md)

---

## 1. Prerequisites

| | |
|---|---|
| .NET SDK | **10.0.301** or a later 10.0.3xx feature band — pinned by [`global.json`](../global.json) with `rollForward: latestFeature` |
| MAUI workload | `dotnet workload install maui` — required for `Oab.App`, all modules, the customer heads, and `Oab.App.Tests` |
| Android | Android SDK + a device or emulator (API 21+) |
| Windows | Windows 10 build 17763 or later, for desk testing |

`Oab.Core`, `Oab.Data`, `Oab.Core.Tests`, `Oab.Data.Tests` and `Oab.TestSupport`
target plain `net10.0` and need **no** workload — which is what lets CI run them
on Ubuntu.

## 2. Shared build configuration

### [`Directory.Build.props`](../Directory.Build.props)

Applies to every project in the repository.

| Property | Value | Effect |
|---|---|---|
| `LangVersion` | `latest` | Collection expressions, primary constructors, partial properties |
| `ImplicitUsings` | `enable` | |
| `Nullable` | `enable` | Nullable reference types everywhere |
| `EnableNETAnalyzers` | `true` | |
| `AnalysisLevel` | `latest` | |
| `EnforceCodeStyleInBuild` | `true` | `.editorconfig` style rules are checked at build; most are `suggestion`, so builds stay green |

It also holds **the single list of package versions** as MSBuild variables:

| Variable | Version | Used by |
|---|---|---|
| `EfCoreVersion` | 10.0.9 | `Oab.Data` (Sqlite + Design) |
| `SqlitePclRawVersion` | 3.0.3 | `Oab.Data` |
| `MvvmToolkitVersion` | 8.4.2 | `Oab.App` |
| `LoggingDebugVersion` | 10.0.0 | customer heads |
| `TestSdkVersion` | 17.14.1 | all test projects |
| `XunitVersion` | 2.9.3 | all test projects |
| `XunitRunnerVersion` | 3.1.4 | all test projects |
| `CoverletVersion` | 6.0.4 | all test projects |

Bump a dependency **there**, not in each csproj.

`Microsoft.Maui.Controls` is deliberately omitted from that list: it rides
`$(MauiVersion)`, which the installed MAUI workload sets for every project at
once.

### Why no Central Package Management

`Directory.Packages.props` (CPM) breaks the MAUI SDK's implicit global usings in
app-head projects, and this repository creates **one head per customer**.
Referencing `$(...)` variables from each csproj gives the same bump-in-one-place
benefit without fighting MAUI. This is a deliberate, documented deviation from
the modern default — see [09 — Decisions](09-decisions.md).

### [`global.json`](../global.json)

```json
{ "sdk": { "version": "10.0.301", "rollForward": "latestFeature" } }
```

Pins the SDK feature band so a developer with a newer preview SDK produces the
same build. `latestFeature` allows patch and feature-band updates within 10.0.3xx.

### [`.editorconfig`](../.editorconfig)

UTF-8, LF endings, final newline, no trailing whitespace. C#: 4-space indent,
**file-scoped namespaces at `warning` severity** (the one style rule that will
actually stop you), system usings first, `var` preferred, expression-bodied
members / primary constructors / switch expressions all at `suggestion`, private
fields `_camelCase`. 2-space indent for csproj/props/xml/xaml/resx/json/yml.
Markdown is exempt from trailing-whitespace trimming (line breaks). EF migrations
are marked `generated_code = true` so style rules do not apply to tool output.

`dotnet format` keeps everything consistent.

## 3. Commands

### Test

```bash
dotnet test tests/Oab.Core.Tests
```

```bash
dotnet test tests/Oab.Data.Tests
```

```bash
dotnet test tests/Oab.App.Tests
```

The first two are fast and cross-platform — this is what CI runs. The third is
MAUI-targeted and **Windows-only**.

### Build and run a customer head

```bash
dotnet build customers/Oab.Customer.Template -f net10.0-windows10.0.19041.0
```

```bash
dotnet build customers/Oab.Customer.Template -f net10.0-android
```

```bash
dotnet publish customers/Oab.Customer.Template -f net10.0-android -c Release
```

### Add a migration

```bash
dotnet ef migrations add <Name> --project src/Oab.Data
```

### Format

```bash
dotnet format
```

## 4. Test inventory

**176 tests, all passing** (verified by running all three suites).

| Suite | Target | Tests | Runs in CI | Covers |
|---|---|---:|---|---|
| [`Oab.Core.Tests`](../tests/Oab.Core.Tests) | `net10.0` | **77** | ✅ | Ledger math incl. correction arithmetic, `LedgerService`, money formatting **and parsing**, summary report |
| [`Oab.Data.Tests`](../tests/Oab.Data.Tests) | `net10.0` | **40** | ✅ | Real SQLite + real migrations, decimal fidelity, newest-first ordering, role filtering, backup/restore, **party editing and archiving, document/line lookup, the batched invoice read** |
| [`Oab.App.Tests`](../tests/Oab.App.Tests) | `net10.0-windows10.0.19041.0` | **59** | ❌ (needs the MAUI workload) | View models: balance→text/colour, role filtering, pay-remaining, statement running balance, the correction flow, backup summary; plus the error log |

### `Oab.App.Tests` breakdown

| File | Tests | Focus |
|---|---:|---|
| `PartyStatementViewModelTests` | 23 | Running balance, ordering, the perspective colour matrix, Arabic labels; and the correction flow — balance moves while history does not, correcting to zero, the document's outstanding following, the five refusals, prompt text |
| `ErrorLogTests` | 17 | Record contents and append order; **never throwing**, even writing to an impossible path; a null exception object; invariant timestamps under `ar-SA`; Arabic round-trip with a BOM; the four trimming rules |
| `PurchasesViewModelTests` | 4 | Credit vs cash listing, pay-remaining, form validation |
| `SuppliersViewModelTests` | 3 | You-owe display, payment settling, role filtering |
| `CustomersViewModelTests` | 4 | Debt/collection cycle, partial payment, role filtering |
| `BackupViewModelTests` | 3 | Localized summary content, empty book, the error-report card staying hidden until something is logged |

### `Oab.Core.Tests` breakdown

| File | Tests | Focus |
|---|---:|---|
| `MoneyInputTests` | 35 | Amount parsing: Arabic-Indic and extended Arabic-Indic digits, `٫` `٬` `،`, bidi marks, mixed digit sets, both cultures each way, round trip against `MoneyFormat` under `en-US` and under the shipping `ar` + Arabic-Indic configuration |
| `LedgerMathTests` | 19 | Sign convention, `CorrectionDelta`, the guards |
| `LedgerServiceTests` | 13 | Every way money can move, incl. adjustments preserving history |
| `LedgerSummaryReportTests` | 5 | Sections, totals, line endings, Arabic-Indic digits |
| `MoneyFormatTests` | 5 | Grouping, decimals, symbol, sign dropping, digit shaping |

`MoneyInputTests` is the largest file in the suite, deliberately. Its bug surface
is "which of a dozen Unicode characters did the keyboard emit", each case costs
one line, and the alternative was four untested copies in page code-behind
(D23).

### `Oab.Data.Tests` breakdown

| File | Tests | Focus |
|---|---:|---|
| `DocumentLookupSqliteTests` | 13 | One document with its lines (decimal quantities, `Number`, offset, empty-not-null, no leakage between invoices); the entries behind an invoice (outstanding falling, standalone payments ignored, **a correction moving the invoice's remainder**, correcting to zero, a cash purchase settled on arrival); and the batched read agreeing with the single read, omitting empty documents, and surviving 1,500 ids |
| `PartyEditingSqliteTests` | 7 | Editing a detached party without duplicating it, `[Flags]` roles surviving, **archiving hiding the party while keeping its balance and history**, un-archiving, edits surviving a restart, and `Update` refusing to insert an unknown party |
| `DatabaseBackupTests` | 6 | Snapshot → damage → restore; the `.pre-restore` copy; junk, missing, and foreign-schema files rejected |
| `LedgerStoreSqliteTests` | 7 | Purchase→payment persisted and balanced, exact decimal round-trip, archived parties hidden, newest-first ordering, `OccurredAt` offset, reopen-the-file durability |
| `PartyRoleFilterTests` | 3 | Supplier/customer separation and the legacy `None`-shows-everywhere rule |

`ErrorLogTests` and `Oab.Data.Tests` both write real files into the temp
directory. That is deliberate in both cases: the value of `ErrorLog` is what it
does when the file system misbehaves, and a fake file system would only prove the
fake works.

### Shared test infrastructure

- [`tests/Oab.Data.Tests/SqliteTestDatabase.cs`](../tests/Oab.Data.Tests/SqliteTestDatabase.cs)
  — a temp directory, a database file with the **real migrations applied**, a
  `LedgerStore` and `LedgerService` over it, `ReopenStore()` for simulating an app
  restart, `PathTo()` for backup targets, and disposal that clears SQLite's
  connection pools first so Windows will let the file go. It lives here rather
  than in `Oab.TestSupport` because that project deliberately references only
  `Oab.Core`. Three test classes carried their own copy of this before; the copy
  in the file you are not looking at is the one that goes stale.
- [`tests/Oab.TestSupport/InMemoryLedgerStore.cs`](../tests/Oab.TestSupport/InMemoryLedgerStore.cs)
  — a full `ILedgerStore` over dictionaries and a list. Shared by `Oab.Core.Tests`
  and `Oab.App.Tests`. It implements the same role-filter rule as the SQLite
  store, including "`None` matches everything", and now the **same newest-first
  ordering** — a fake that returns a different order from the real store lets a
  screen pass its tests and still show a shopkeeper their entries backwards.
- [`tests/Oab.App.Tests/TestInfrastructure.cs`](../tests/Oab.App.Tests/TestInfrastructure.cs)
  — `FakePreferences` (an in-memory `IPreferences`, so localization needs no
  device) and `VmContext`, which assembles the **real** dependency graph a module
  view model expects: `InMemoryLedgerStore`, a real `LedgerService`, a real
  `LocalizationManager`, a real `MoneyFormatter`, and a `ShopConfig` of
  `"Test Shop" / "SP" / en`. English keeps label assertions deterministic; one
  test passes `culture: "ar"` to check the Arabic path.

`Oab.Data.Tests` uses **no fakes at all** — every test writes a real `.db` file
into the temp directory, runs the real migrations, and cleans up in `Dispose`
(tolerating a stray Windows file handle rather than failing the run).

### The `Oab.App.Tests` trick

A MAUI-targeted **library** (`OutputType=Library`, `EnableDefaultMauiItems=false`,
`WindowsPackageType=None`) targeting a single platform. That is enough for the
view models — which touch `Colors`, `FlowDirection` and `IPreferences` — to be
exercised headlessly by the ordinary test runner, with no emulator and no UI.
It is the reason presentation logic can be asserted at all.

## 5. Continuous integration

[`.github/workflows/ci.yml`](../.github/workflows/ci.yml) — on every push and PR
to `main`:

1. `actions/checkout@v4`
2. `actions/setup-dotnet@v4` with `10.0.x`
3. `dotnet test tests/Oab.Core.Tests -c Release`
4. `dotnet test tests/Oab.Data.Tests -c Release`

Runs on `ubuntu-latest`. Only the pure logic layers are built, so CI stays fast
and reliable without Android/MAUI workloads — and it covers the part that must
never be wrong: money.

**`Oab.App.Tests` is not in CI.** Run it locally on Windows before pushing UI
changes. Adding a `windows-latest` job with `dotnet workload install maui` would
close that gap at the cost of a much slower pipeline.

## 6. Known build output

```
dotnet build OAB.slnx
    0 Warning(s)
    0 Error(s)
```

**Zero is the expected number, and the reason is not tidiness.** Three warnings
used to print on every build. All three were understood and harmless, which is
exactly what makes them dangerous: a build whose output is always the same noise
is a build nobody reads, and this project has already lost two working screens to
a problem the tools were reporting to an empty room (D21, D22). A fourth warning
appearing among three permanent ones is invisible; appearing alone it is obvious.

| Was | Where | Resolution |
|---|---|---|
| `EF1002: ExecuteSqlRawAsync inserts interpolated strings directly into the SQL` | [`DatabaseBackupService.cs`](../src/Oab.Data/Backup/DatabaseBackupService.cs) | **Suppressed** at that one statement with `#pragma` and the reasoning inline: `VACUUM INTO` cannot take a parameter, and the path is app-originated and single-quote-escaped (D11). |
| `CS8602: Dereference of a possibly null reference` | [`NewPurchaseViewModel.cs`](../src/Oab.Modules/Oab.Modules.Purchases/NewPurchaseViewModel.cs) | **Restructured, not suppressed.** The supplier is now chosen in one expression above the `try`, so the compiler can see what the reader could. |
| `MA002: UseMaui no longer implies Microsoft.Maui.Controls` | [`Oab.App.Tests.csproj`](../tests/Oab.App.Tests/Oab.App.Tests.csproj) | **Fixed.** The package is referenced explicitly on `$(MauiVersion)` instead of arriving transitively through `Oab.App`. |

If you add a suppression, put the reason next to it. A `#pragma` without a
sentence is how the next person ends up re-deciding this from scratch.

The MSBuild message about `MauiXamlInflator=SourceGen` on every MAUI project is
informational — XAML is compiled at build time rather than inflated at runtime.

## 6a. Reading the error log after a run

Since the global exception handler landed
([04 §9](04-app-shell.md#9-diagnostics--making-a-crash-leave-evidence)), a failure
during desk testing leaves a file instead of vanishing. On Windows:

```bash
cat "$LOCALAPPDATA/User Name/com.companyname.oab.customer.template/Data/errors.log"
```

On a device, use the **Send error report** card on the backup screen. The path is
always `FileSystem.AppDataDirectory/errors.log`; the folder name follows the
head's `ApplicationId`, so it changes per shop.

**Read it after any manual test session, even one that looked fine.** The first
run after installation produced a `NotSupportedException` on two screens that had
been reported as working.

## 7. Platform targets

| Target framework | Min version | Status |
|---|---|---|
| `net10.0-android` | API 21 | **Primary.** Shopkeepers already own Android phones. |
| `net10.0-windows10.0.19041.0` | 10.0.17763.0 | Desk testing and the view-model test host. |

The customer template still contains `Platforms/iOS/` and
`Platforms/MacCatalyst/` folders from `dotnet new maui`, and the csproj carries
`SupportedOSPlatformVersion` conditions for both. **Neither is in
`TargetFrameworks`, so neither is built.** They are inert scaffolding; removing
them is safe cleanup.

## 8. Release — what is missing

Today's Android output is a **debug-signed APK**. Before anything reaches a real
shop:

| Item | Status |
|---|---|
| Release keystore + documented handling | ❌ not set up |
| Signed release APK | ❌ |
| Per-shop `ApplicationId` | ❌ template still `com.companyname.oab.customer.template` |
| Real icon, splash, Arabic app name | ❌ still the .NET template art |
| Version scheme across shops | ❌ undefined |
| Upgrade test with real data (install v1 → enter data → install v2 with a new migration) | ❌ **the `Database.Migrate()` promise is untested against real data** |
| `INTERNET` / `ACCESS_NETWORK_STATE` permissions removed from the manifest | ❌ still present from the template; the app makes no network calls |

That last permission item matters beyond tidiness: an offline-first ledger app
requesting internet access is exactly the kind of thing that makes a shopkeeper
hesitate.

---

Next: [09 — Design Decisions](09-decisions.md)
