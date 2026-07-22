# OAB — Technical Documentation

Complete reference for the OAB codebase as it exists today: what is built, how
it fits together, why it is shaped this way, and what is deliberately missing.

> **Scope note.** These documents describe **implemented code**, verified by
> reading every source file in the repository. Anything not yet built is
> confined to [10 — Implementation Status](10-status.md) and clearly labelled.
> Forward-looking plans live in [`plan.md`](../plan.md) and
> [`ROADMAP.md`](ROADMAP.md), not here.

---

## The product in one paragraph

OAB is an offline-first ledger app for small souk shops — resellers of shampoo,
makeup, or clothing who buy in bulk from distributors and currently track
everything in a paper notebook. It answers three questions: *what do I owe my
suppliers*, *what do my customers owe me*, and *is it paid?* It is deliberately
**less** than a point-of-sale system, because shops avoid POS systems for being
complicated, not for being expensive. Each shop receives a build containing
only the features they asked for. Arabic-first, right-to-left, Android-first,
no internet required, ever.

---

## Documentation map

| # | Document | Answers |
|---|---|---|
| 01 | [Architecture](01-architecture.md) | How the layers fit, what depends on what, what happens at startup |
| 02 | [The Money Engine](02-money-engine.md) | The append-only ledger, sign convention, every `LedgerService` method, worked examples, money in and out of a string |
| 03 | [Data Layer](03-data-layer.md) | SQLite schema, EF Core mapping, migrations, the backup/restore machinery |
| 04 | [App Shell](04-app-shell.md) | `OabApp`, `OabShell`, `ShopConfig`, localization internals, crash logging, the party statement screen |
| 05 | [Feature Modules](05-modules.md) | Every screen in the product, and how to write a new module |
| 06 | [Localization & RTL](06-localization.md) | The full resource-key catalogue, live language switching, Arabic-Indic digits |
| 07 | [Per-Shop Customization](07-customization.md) | Onboarding a new shop end to end, every `ShopConfig` knob |
| 08 | [Build, Test & Release](08-build-test-release.md) | Every command, the test inventory, CI, versioning |
| 09 | [Design Decisions](09-decisions.md) | ADR-style record of the choices and their consequences |
| 10 | [Implementation Status](10-status.md) | Honest inventory: what is built, what is stubbed, what is missing, known risks |

### Suggested reading order

- **New to the codebase:** 01 → 02 → 04 → 05.
- **Onboarding a shop:** 07 → 06 → 08.
- **Touching money:** 02 → 03 → 09.
- **Deciding what to build next:** 10 → [`ROADMAP.md`](ROADMAP.md).

---

## Fact sheet

| | |
|---|---|
| **Platform** | .NET 10 · .NET MAUI · Android (primary) + Windows (desk testing) |
| **Storage** | SQLite via EF Core 10.0.9, one file per shop, on-device only |
| **MVVM** | CommunityToolkit.Mvvm 8.4.2 |
| **Tests** | xUnit — **152 tests, all passing** (77 core · 16 data · 59 view-model + error log) |
| **Solution projects** | 3 libraries + 4 feature modules + 1 customer head + 4 test projects |
| **Migrations** | 2 (`InitialCreate`, `AddPartyRoles`) |
| **Localized strings** | 81 keys × 2 languages (English, Arabic) |
| **Screens** | 6 (Purchases list, New purchase, Suppliers, Customers, Backup, Party statement) — plus the correction flow, which is dialogs over the statement |
| **Network use** | None. The app never makes a network call. |

## The three rules

Everything in this codebase follows from three commitments. They are repeated
in each document where they bite, and justified in [09 — Design
Decisions](09-decisions.md).

1. **The ledger is append-only.** Every money movement is an immutable, signed
   `LedgerEntry`. Balances are always `SUM(entries)`. Corrections are new
   `Adjustment` entries carrying a mandatory note — never edits, never deletes.
   There is no mutable balance column anywhere, and there never should be. The
   shopkeeper's way in is tapping an entry on the party statement and saying what
   it should have been ([04 §10](04-app-shell.md#the-correction-flow)).

2. **Custom work never touches core.** A shop that wants a special feature gets
   a new `IOabModule` in *their* customer folder. If a second shop wants it, it
   is promoted to `src/Oab.Modules/`. The repository is never forked per
   customer.

3. **Every build ships `BackupModule`.** The book lives on one phone with no
   sync. Without backup, a lost phone loses everything — strictly worse than the
   paper notebook being replaced.

### And one habit

**Nothing fails silently.** Every `async void` handler runs through
`Page.RunSafelyAsync`; anything that escapes is caught by process-wide handlers
and written to a shareable `errors.log`
([04 §9](04-app-shell.md#9-diagnostics--making-a-crash-leave-evidence)). On a
shopkeeper's phone there is no console and no network, so a crash that leaves no
record is a bug that can never be fixed. Within 25 seconds of this landing it
revealed that two of the six screens had never once loaded successfully.

### And one shortcut that keeps being wrong

**A rule copied into two screens is a rule nobody tests.** Nobody writes a test
for a private helper in a page's code-behind, so duplicated logic is untested by
construction — that is how four amount parsers came to exist, none of which could
read the digits the app itself prints ([09 D23](09-decisions.md#d23--one-amount-parser-in-core-mapping-separators-per-culture)).
When a rule appears twice, it belongs in `Oab.Core`, where it has no dependencies
and a test costs one line.
