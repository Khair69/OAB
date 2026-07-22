# 09 — Design Decisions

[← 08 Build, Test & Release](08-build-test-release.md) · [Index](README.md) · Next: [10 — Implementation Status](10-status.md)

---

Every non-obvious choice in the codebase, with the reasoning and the price paid.
Read this before changing anything structural — most of these decisions look
arbitrary until you know what they were protecting against.

---

## D1 — The ledger is append-only

**Context.** A shop needs balances, partial payments, history, corrections, and
eventually multi-device sync.

**Decision.** Model money as an append-only ledger of immutable signed entries.
Balances are always `SUM(entries)`. There is no mutable balance column anywhere,
and `ILedgerStore` exposes no update or delete for entries.

**Why.** Every other feature falls out for free: partial payments are just more
entries; "is this invoice paid?" is a sum equal to zero; history is the table
itself; undo is a new entry. It also matches how a paper notebook actually works
— you cross out, you do not erase — which is the mental model the user already
has. And append-only data merges cleanly, which is what will make sync cheap
later.

**Cost.** Every balance is a computation, not a read. See D6 for how that is
paid for.

**Enforcement.** The interface shape, not a convention. To violate this rule a
contributor would first have to widen `ILedgerStore` — a visible act in review.

---

## D2 — One `Party`, not `Supplier` and `Customer`

**Context.** In the souk, the same person is a supplier one day and a customer
the next.

**Decision.** A single `Party` entity. What someone "is" is derived from the
ledger, never declared.

**Why.** Two entity types would mean two balances for one human, and a
reconciliation problem the shopkeeper never asked for. With one party, a person
you buy from and sell to nets to a single honest number.

**Cost.** The supplier list and customer list need *some* way to stay separate,
which is D3.

---

## D3 — `PartyRole` is a UI hint, and `None` matches everything

**Decision.** A `[Flags]` enum on `Party` used **only** for list filtering.
Balances ignore it entirely. `PartyRole.None` matches every filter.

**Why the flags:** a party can genuinely be both. **Why `None` matches
everything:** the `Roles` column was added in a migration with default `0`. If
`None` matched nothing, every party in every existing shop's database would
vanish from every list the moment they upgraded. Instead they all keep appearing
exactly where they did.

**Cost.** The filter cannot be translated to SQL, so it runs in memory
([03 §4](03-data-layer.md#client-side-evaluation-and-why)). Party counts
are small; this is the right trade.

**Pinned by** `PartyRoleFilterTests.UntaggedLegacyParty_ShowsInEveryList`.

---

## D4 — Signed amounts, with direction phrased in words

**Decision.** `Amount` is signed — positive = the party owes the shop. But
`MoneyFormat` renders the **magnitude only**, and every screen phrases direction
in words ("You owe", "They owe you", "Settled").

**Why.** One signed number keeps the data model trivially simple. But a minus
sign in front of a number in an RTL layout is ambiguous at a glance, and colour
alone is a bad carrier of meaning (colour-blindness, sunlight on a cheap screen,
and — critically — red means opposite things on the supplier and customer
screens). Words carry the meaning; colour is only an accelerant.

**Consequence.** `PartyStatementViewModel` needs a `perspective` parameter
telling it which list opened it, so the colour matches the row that was tapped.
See [04 §10](04-app-shell.md#wording-and-colour).

---

## D5 — A cash purchase writes two entries

**Decision.** `RecordPurchaseAsync(..., paidNow: true)` appends both a
`Purchase −amount` and a `PaymentOut +amount`.

**Why.** There is then no such thing as a "cash purchase" anywhere downstream —
only a purchase that happens to be settled. No branch in any list, report,
statement, or future sync has to know the difference. And a later correction or
partial refund has real entries to attach to.

**Cost.** Two rows where one might do. Storage is free; special cases are not.

---

## D6 — Money is `decimal`, stored as SQLite TEXT

**Decision.** `decimal` in C#, `TEXT` columns in SQLite.

**Why.** SQLite has no decimal type. Its `REAL` is an IEEE-754 double, and a
double cannot represent `0.1` exactly — a ledger that drifts by fractions of a
currency unit is a ledger nobody trusts.

**Cost.** SQLite cannot `SUM()` those columns, so `LedgerStore` projects amounts
and sums them in C#. That is why `GetBalancesAsync` reads the whole entries table
— acceptable at shop scale, and the first thing to revisit at 5,000+ entries.

**Pinned by** `LedgerStoreSqliteTests.DecimalAmounts_SurviveRoundTrip_Exactly`
(`0.1 + 0.2 == 0.3` exactly).

---

## D7 — Product-line architecture, never a fork per customer

**Decision.** One repository. Shared core. One ~40-line composition project per
shop. A custom feature is a new `IOabModule` in that shop's folder, promoted to
`src/Oab.Modules/` if a second shop wants it.

**Why.** Forking per customer drowns you after about five shops: a bug fixed in
one copy has to be found and re-fixed in the others, forever. This structure
makes "custom build per shop" cost a config file rather than a codebase.

**Cost.** The shell must be feature-blind, which rules out hardcoding any
navigation. `OabShell` builds its flyout at runtime from whatever modules it is
handed.

**Unproven.** The promise is "new shop in under an hour". Only one head exists;
this has not been measured. See [07 §7](07-customization.md#7-where-the-customization-thesis-is-still-unproven).

---

## D8 — `Oab.Core` has zero package references

**Decision.** The money engine is a plain `net10.0` library depending on nothing.

**Why.** It must be testable in milliseconds with no runtime, no database and no
device — and it is the one part that must never be wrong. It is also the part
most likely to be reused if the UI is ever replaced.

**Consequence.** `LedgerSummaryReport` takes its wording as a `SummaryLabels`
parameter rather than looking it up, because a resource lookup would drag a
dependency into Core. The app layer supplies the localized text.

---

## D9 — No value converters; view models render text and colour

**Decision.** View models produce immutable row records with pre-rendered
`string` and `Color` properties. There is not a single `IValueConverter` in the
codebase.

**Why.** Formatting is where the money bugs hide — sign, currency, digit
shaping, which direction is alarming. In a converter it is essentially
untestable; in a view model, `Oab.App.Tests` asserts on
`"You owe them: 100.00 SP"` and `Colors.Firebrick` with no UI at all.

**Cost.** Rows must be rebuilt on reload rather than re-bound. `LoadAsync`
clearing and repopulating the `ObservableCollection` is the standard pattern
here.

---

## D10 — Migrate in the `OabApp` constructor

**Decision.** `db.Database.Migrate()` runs before any page can exist.

**Why.** Upgrades become automatic: a shop installs a new APK and the schema is
current before the first screen loads. There is no migration screen, no "please
wait", and no code path where a page can see a stale schema. Local SQLite makes
it fast even on an old phone.

**Cost.** A migration failure is a startup crash. There is no recovery UI —
which raises the stakes on testing migrations against real data before shipping
([08 §8](08-build-test-release.md#8-release--what-is-missing)).

---

## D11 — Backup uses `VACUUM INTO`, restore keeps a `.pre-restore` copy

**Decision.** Snapshots via `VACUUM INTO`, not `File.Copy`. Restore validates
the file first, keeps a copy of what it is about to destroy, clears connection
pools, deletes the `-wal`/`-shm` sidecars, then migrates.

**Why.** `File.Copy` on a live SQLite database can catch a half-written page or
miss the journal, producing a backup that is *silently* corrupt — the worst
possible failure for the feature whose entire job is to be there when everything
else is gone. `VACUUM INTO` produces a consistent copy while the app still holds
the database open. Every subsequent step in restore exists because skipping it
corrupts something: stale WAL files belong to the old database; pooled handles
prevent replacement on Windows; an older backup needs migrating up.

**Cost.** The interpolated SQL triggers `EF1002` (the statement cannot be
parameterised; the path is escaped and app-originated).

---

## D12 — Two kinds of backup, because they fail differently

**Decision.** Ship both a `.db` snapshot and a plain-text summary.

**Why.** The `.db` restores perfectly but only into this app — useless if the
phone is gone and the APK is not to hand. The text summary cannot be restored,
but a person can read it on any phone, forward it, or copy it back into a paper
notebook. Between them, there is no scenario where the shop is left with
nothing.

**Detail.** The text file is written UTF-8 **with BOM** so Arabic opens
correctly in Windows Notepad and most Android viewers, and uses `'\n'`
explicitly so the bytes are identical on every platform.

---

## D13 — The party statement lives in `Oab.App`, not in a module

**Decision.** `PartyStatementPage` is in the shell, and both list modules push
it via a static `PushAsync` helper.

**Why.** A party is often supplier and customer at once, and both lists open the
same page. Putting it in `SupplierDebts` would force `CustomerDebts` to reference
it, breaking the rule that modules never reference each other. Putting it in both
would duplicate the one screen where correctness matters most.

**Consequence.** The page must be perspective-neutral and take the caller's
`PartyRole` as a parameter (D4).

---

## D14 — Two timestamps on every entry

**Decision.** `OccurredAt` (`DateTimeOffset`, local offset preserved) and
`CreatedAtUtc` (`DateTime`, device clock).

**Why.** A shopkeeper backdates a purchase they forgot to log. The statement must
read in the order money actually moved (`OccurredAt`), but two entries can share
an `OccurredAt` — a cash purchase produces exactly that — so a stable tiebreaker
is needed (`CreatedAtUtc`). `CreatedAtUtc` is also the natural basis for
append-order and future sync.

**Pinned by**
`PartyStatementViewModelTests.EntriesOutOfChronologicalOrderAreStillSummedInOrder`.

---

## D15 — No Central Package Management

**Decision.** Package versions are MSBuild `$(...)` variables in
`Directory.Build.props`, not `Directory.Packages.props`.

**Why.** CPM breaks the MAUI SDK's implicit global usings in app-head projects,
and this repository creates one head per customer — so the breakage would be
per-shop and permanent. The variables give the same bump-in-one-place benefit.

**Cost.** A deviation from the modern .NET default that will look like an
oversight to a new contributor. Hence this entry, and the comment in the props
file.

---

## D16 — CI builds only the pure layers

**Decision.** GitHub Actions runs `Oab.Core.Tests` and `Oab.Data.Tests` on
Ubuntu. `Oab.App.Tests` is Windows-only and local.

**Why.** Those two suites cover the part that must never be wrong — money — and
they need no workloads, so CI is fast and does not flake on Android SDK
installs. A slow or flaky pipeline gets ignored, and an ignored pipeline
protects nothing.

**Cost.** Presentation regressions are caught only by whoever remembers to run
the third suite. Adding a `windows-latest` job would close this at a real cost in
pipeline time.

---

## D17 — `OabServices`, a deliberate service locator

**Decision.** A static `OabServices.Get<T>()`, set once at startup.

**Why.** MAUI does not constructor-inject pages pushed from code-behind, nor
anything reached from a `DisplayPromptAsync` flow. The alternatives — passing a
provider down through every page, or a navigation abstraction — cost more
complexity than they remove for a two-call-site problem.

**Discipline.** It is used in exactly two places (`PurchasesListPage`,
`PartyStatementPage.PushAsync`), it throws a clear message if used before
startup, and its XML doc names it an escape hatch. If it reaches five call
sites, replace it with a navigation service.

---

## D18 — Arabic default, RTL bound from the root

**Decision.** `DefaultCulture = "ar"`; `OabShell` **binds** `FlowDirection` to
the localization manager rather than setting it.

**Why.** RTL cannot be retrofitted — every layout assumption has to be right
from the first screen or the app has to be rebuilt. Binding rather than setting
is what makes the language switch instant, and an instant switch is what lets a
shopkeeper hand the phone to someone who reads the other language.

**Mechanism.** `PropertyChanged("Item")` invalidates every indexer binding at
once, so all `{oab:Tr …}` text re-resolves without a restart.

---

## D19 — A correction is entered as "what it should have been"

**Context.** `RecordAdjustmentAsync` takes a **pre-signed** amount: `+900` to fix
a purchase of 1000 that should have been 100. That is the correct data model and
an impossible thing to ask a shopkeeper for. They know the entry is wrong and
they know the right number; they do not know the delta, and they certainly do not
know its sign.

**Decision.** The UI asks for the corrected **magnitude**. `LedgerMath.CorrectionDelta`
takes the direction from the entry being corrected and computes the delta:

```csharp
var corrected = recordedAmount < 0m ? -correctedMagnitude : correctedMagnitude;
return corrected - recordedAmount;
```

A typo changes *how much* moved, never *which way* — so direction is never the
typist's to enter, and a negative input throws rather than being interpreted.

**Consequences.**

- **Zero is a legal answer** and means "this never happened": the delta cancels
  the entry exactly while leaving it on the record. Append-only has no delete,
  and logging the same purchase twice is a real mistake that needs a real answer.
- **A delta of zero is not an error but must not be posted** —
  `RecordAdjustmentAsync` throws on it. `CorrectAsync` returns
  `AlreadyThatAmount`, which is what turns a crash into a sentence.
- **The adjustment inherits the corrected entry's `DocumentId`**, so an invoice's
  outstanding follows the correction. Omitting it would leave the party balance
  right and the purchases list wrong — a disagreement between two screens costs
  more trust than the original typo did.
- **It is stamped `Now`, not the original date.** The book *was* wrong for those
  days, and the statement exists to explain exactly that.

**Cost.** One more concept in `LedgerMath`, and a magnitude→delta translation
that has to be right. It is seven lines in Core with ten test cases, which is
where a rule like this belongs.

---

## D20 — Corrections open from a tap and an action sheet, not a long-press

**Context.** The natural gesture for "do something to this row" is a long-press.
MAUI has no long-press gesture, and `FlyoutBase.ContextFlyout` is Windows and
macOS only — Android, the actual product, gets nothing.

**Decision.** A `TapGestureRecognizer` on the statement row opens a
`DisplayActionSheetAsync` with one named option.

**Why not the alternatives.** `CommunityToolkit.Maui` has `TouchBehavior`
with a long-press command, but that is a whole package for one gesture on one
screen. A platform handler is a per-platform surface to maintain in a repo whose
whole thesis is being small. `SwipeView` works on Android but its direction flips
under RTL, and this app is RTL by default.

**Why it is better anyway.** Nothing on the statement announces that a row is
tappable, so a hidden long-press would be a feature nobody ever finds. The action
sheet is where the feature is *discovered* — it names the action in the
shopkeeper's language — and it is also what keeps a stray tap harmless.

**Cost.** One extra dialog, on a rare and consequential action, where an extra
confirmation is not a tax. Revisit if the row ever needs a second action; a real
long-press earns its dependency then.

---

## D21 — The global handler records; it does not recover

**Context.** MAUI event handlers must be `async void`, and an exception escaping
one goes straight to the process. On a shopkeeper's phone that means the app
disappears mid-tap, and the only available bug report is *"it closed"* — no
console, no debugger, no crash reporter, no network.

**Decision.** Two layers. Every `async void` handler funnels through
`Page.RunSafelyAsync`, which **catches, logs, and shows a message**. Everything
that still escapes — a background task nobody awaited, a throw on a thread with
no page, the startup path itself — is caught by process-wide handlers installed
in `UseOab` before `UseMauiApp`, which **log and let the process die**.

**Why the second layer does not swallow.** `WinUI`'s handler can set
`e.Handled = true` and keep the app alive. It deliberately does not. By the time
a global handler runs, the exception has already unwound past every place that
knew what was being attempted; the app's state is unknown. A ledger that carries
on in an unknown state can write a wrong number, and a wrong number is worse than
a closed app — the whole product is "every number is explainable". The goal of
the second layer is not survival, it is that **the next launch can explain what
happened**.

**Why it is installed first.** `Database.Migrate()` runs in the `OabApp`
constructor (D10), where a failure is a startup crash with no recovery UI. If the
handler were installed after `UseMauiApp`, the single worst failure mode in the
product would be the one it did not cover.

**Why the logger can never throw.** Every caller is a catch block, several of
them on a dying process. `ErrorLog` swallows its own failures — the only empty
`catch` in the codebase — because a logger that can fail turns a handled error
into the crash it was written to prevent.

**Why a static `ErrorLog.Current` as well as a DI registration.**
`RunSafelyAsync` is a static extension method and the process-wide handlers run
where there is no DI scope to ask. Both point at the **same instance**, so the
backup screen shares the file and the lock. Every use is null-conditional, so a
view model constructed directly in a test logs nowhere.

**Cost.** A second static alongside `LocalizationManager.Current`, and an empty
`catch` that will look like a mistake to anyone who has not read this entry.

**Immediate return.** Installed, launched once on Windows, and 25 seconds later
the log named a `NotSupportedException` that had been thrown on **every open of
the purchases list and the party statement** — two of six screens, broken, with
nothing saying so. See
[04 §9](04-app-shell.md#what-it-found-immediately).

---

## D22 — Newest-first ordering happens in C#, not SQL

**Context.** `GetDocumentsAsync` and `GetEntriesForPartyAsync` ordered by
`OccurredAt` in the query. SQLite has no `DateTimeOffset` type, and EF Core does
not silently fall back to client evaluation for it — it **rejects the query at
translation time**.

**Decision.** Filter in SQL, order in C#.

**Why this was invisible.** The two methods had no test against real SQLite;
the view-model tests use `InMemoryLedgerStore`, which runs LINQ-to-Objects and
cannot reproduce a translation failure. The rule that follows:
**a store method with no real-SQLite test has never actually run.**

**Cost.** The `Documents.OccurredAt` index no longer serves the ordering. It is
close to free anyway — the `WHERE` still runs server-side, so what gets sorted is
one shop's purchases or one party's entries, never the table. Same family as D6:
SQLite's type system is thin, and the price is paid in C#.

---

## Rejected alternatives, briefly

| Considered | Rejected because |
|---|---|
| **Avalonia** for the UI | Better desktop story, weaker mobile. Android is the product. |
| **Blazor Hybrid** | Good if you want HTML/CSS skinning per shop; adds a web runtime for no benefit the module system does not already give. |
| **A stored `Balance` column** | A second source of truth that will drift from the entries. The whole design exists to avoid it. |
| **Separate `Supplier` / `Customer` tables** | Two balances for one human (D2). |
| **`PurchaseOnCredit` / `PurchaseCash` as distinct kinds** (as sketched in `plan.md`) | Superseded by D5 — a cash purchase is a settled credit purchase, and the smaller enum has fewer states to get wrong. |
| **Fork per customer** | Drowns after ~5 shops (D7). |
| **Editing an entry in place** | The one thing the append-only rule exists to prevent. A crossed-out line in a notebook is still readable; an erased one is not (D1, D19). |
| **`CommunityToolkit.Maui` for a long-press gesture** | A package for one gesture on one screen (D20). |
| **In-memory EF provider for data tests** | Would not have caught the decimal-storage, WAL-sidecar, or `ORDER BY DateTimeOffset` problems. The data tests use real files (D22). |
| **A base `OabPage` class carrying the exception funnel** | Every page in every module would have to inherit it. `RunSafelyAsync` is an extension method so a module can hold a plain `ContentPage` (D21). |
| **Swallowing the exception in the global handler to keep the app alive** | Carrying on in an unknown state can write a wrong number, which is worse than closing (D21). |
| **A third-party crash reporter** | Needs a network. The product's defining constraint is that it works offline, indefinitely. |

---

Next: [10 — Implementation Status](10-status.md)
