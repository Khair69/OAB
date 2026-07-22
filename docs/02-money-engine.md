# 02 — The Money Engine (`Oab.Core`)

[← 01 Architecture](01-architecture.md) · [Index](README.md) · Next: [03 — Data Layer](03-data-layer.md)

---

`Oab.Core` is a `net10.0` class library with **zero package references**. It
contains the entire definition of what money means in this product. Everything
else in the repository — database, screens, backups — is a way of storing or
displaying what this project decides.

```
src/Oab.Core/
├── Domain/
│   ├── Party.cs          Anyone the shop exchanges money with
│   ├── PartyRole.cs      [Flags] Supplier / Customer — a UI hint only
│   ├── LedgerEntry.cs    One immutable, signed money movement
│   ├── EntryKind.cs      Purchase / Sale / PaymentOut / PaymentIn / Adjustment
│   └── Document.cs       Optional grouping (an invoice) + DocumentLine
├── Ledger/
│   ├── ILedgerStore.cs   Persistence boundary — note: no update/delete for entries
│   ├── LedgerMath.cs     Pure functions: Balance, Outstanding, IsSettled,
│   │                     SignedAmount, CorrectionDelta
│   └── LedgerService.cs  Use-case layer: every way money can move
├── Formatting/
│   └── MoneyFormat.cs    decimal → string, incl. Arabic-Indic digit shaping
└── Reporting/
    └── LedgerSummaryReport.cs   The whole book as human-readable plain text
```

---

## 1. The sign convention

**One number carries direction.** `LedgerEntry.Amount` is signed:

> **Positive = the party owes the shop.  Negative = the shop owes the party.**

This is the single most important sentence in the codebase. It is why the
supplier list and the customer list are the same query with opposite
interpretations, and why a person who is both a supplier and a customer nets out
to one honest number.

| `EntryKind` | Meaning | Sign applied | Effect on balance |
|---|---|---|---|
| `Purchase` (1) | Shop bought goods from the party | `-amount` | Shop owes them more |
| `Sale` (2) | Shop sold goods to the party | `+amount` | They owe the shop more |
| `PaymentOut` (3) | Shop paid money to the party | `+amount` | Shop owes them less |
| `PaymentIn` (4) | Party paid money to the shop | `-amount` | They owe the shop less |
| `Adjustment` (5) | Manual correction | **pre-signed by caller** | Either direction |

`Adjustment` is the only kind whose amount is supplied already signed.
`LedgerMath.SignedAmount` throws `ArgumentException` if you try to pass an
adjustment through it — the mistake is caught at the boundary rather than
producing a silently wrong correction.

## 2. Domain entities

### `Party` — [`Party.cs`](../src/Oab.Core/Domain/Party.cs)

Anyone the shop exchanges money with. There is **no separate Supplier and
Customer type**, because in the souk the same person is a supplier one day and a
customer the next. What someone "is" is derived from the ledger, not declared.

| Member | Type | Notes |
|---|---|---|
| `Id` | `Guid` | Defaults to `Guid.NewGuid()`. Guid, not int, so future multi-device sync needs no ID negotiation. |
| `Name` | `required string` | Indexed in SQLite. |
| `Phone` | `string?` | Modelled and persisted; **not yet exposed in any screen**. |
| `Note` | `string?` | Same — persisted, not yet surfaced. |
| `Roles` | `PartyRole` | Which lists they appear in. A UI hint; balances ignore it. |
| `IsArchived` | `bool` | Hidden from lists but kept for history. **Parties are never deleted.** No screen sets this yet. |

There is deliberately **no `Balance` property**. A stored balance is a second
source of truth that can drift from the entries; balances are always computed.

### `PartyRole` — [`PartyRole.cs`](../src/Oab.Core/Domain/PartyRole.cs)

```csharp
[Flags] public enum PartyRole { None = 0, Supplier = 1, Customer = 2 }
```

The matching rule is one method, and it carries a legacy guarantee:

```csharp
public static bool MatchesFilter(this PartyRole roles, PartyRole wanted) =>
    roles == PartyRole.None || (roles & wanted) != 0;
```

**`None` matches every filter.** Rows created before the `Roles` column existed
default to `None`, so an upgraded database never loses a party from a list. This
rule is pinned by `PartyRoleFilterTests.UntaggedLegacyParty_ShowsInEveryList`.

### `LedgerEntry` — [`LedgerEntry.cs`](../src/Oab.Core/Domain/LedgerEntry.cs)

One immutable line in the book. Entries are only ever appended.

| Member | Type | Notes |
|---|---|---|
| `Id` | `Guid` | |
| `PartyId` | `Guid` | Required FK. Every entry belongs to somebody. |
| `DocumentId` | `Guid?` | Set when the entry belongs to or settles a document. |
| `OccurredAt` | `DateTimeOffset` | When it happened *as the shopkeeper saw it* — local time is preserved via the offset. This is what statements sort by. |
| `CreatedAtUtc` | `DateTime` | When the row was written on the device. Defaults to `DateTime.UtcNow`. Basis for append-only ordering and future sync; used as the tiebreaker when two entries share an `OccurredAt`. |
| `Amount` | `decimal` | Signed. Never `double` — see [09 — Decisions](09-decisions.md). |
| `Kind` | `EntryKind` | |
| `Note` | `string?` | Mandatory in practice for adjustments (enforced by `LedgerService`). |

Two timestamps rather than one is a deliberate choice: a shopkeeper can backdate
a purchase they forgot to log, and the statement must still read in the order
money actually moved, with a stable tiebreak.

### `Document` and `DocumentLine` — [`Document.cs`](../src/Oab.Core/Domain/Document.cs)

An optional grouping: "the crate of shampoo I bought Tuesday".

```csharp
public enum DocumentKind { Purchase = 1, Sale = 2 }
```

| `Document` member | Type | Notes |
|---|---|---|
| `Id`, `PartyId`, `Kind`, `OccurredAt` | | |
| `Number` | `string?` | The supplier's invoice number, if the shopkeeper wants it. Not surfaced in the UI yet. |
| `Note` | `string?` | |
| `Lines` | `List<DocumentLine>` | Optional. Shops that track only money leave this empty. |

| `DocumentLine` member | Type | Notes |
|---|---|---|
| `Id`, `DocumentId` | `Guid` | |
| `Description` | `string` | |
| `Quantity`, `UnitPrice` | `decimal` | |
| `Total` | `decimal` | Computed (`Quantity * UnitPrice`), **not persisted** — `Ignore`d in the EF model. |

**A document never carries money state itself.** Whether it is paid lives in the
ledger entries that reference it:

> `IsPaid(doc)` ⟺ `SUM(entries where DocumentId == doc.Id) == 0`

That is why a cash purchase writes *two* entries (the debt and its settlement)
rather than one — the document's history stays honest, and a later partial
refund or correction has something to attach to.

## 3. `LedgerMath` — the pure functions

[`LedgerMath.cs`](../src/Oab.Core/Ledger/LedgerMath.cs). Every money fact the UI
shows should come through here, so the sign convention lives in exactly one
place.

| Function | Signature | Behaviour |
|---|---|---|
| `Balance` | `(IEnumerable<LedgerEntry>) → decimal` | `entries.Sum(e => e.Amount)`. Signed. |
| `Outstanding` | `(IEnumerable<LedgerEntry>) → decimal` | `Math.Abs(sum)`. Always positive, so a purchase document and a sale document report "150 remaining" the same way. `0` = settled. |
| `IsSettled` | `(IEnumerable<LedgerEntry>) → bool` | `sum == 0m`. |
| `SignedAmount` | `(EntryKind, decimal positive) → decimal` | Applies the table in §1. Throws `ArgumentOutOfRangeException` if the amount is ≤ 0, `ArgumentException` for `Adjustment`. |
| `CorrectionDelta` | `(decimal recorded, decimal correctedMagnitude) → decimal` | The signed adjustment that makes an existing entry count as `correctedMagnitude`. See below. |

`SignedAmount` refusing non-positive input is what stops a `-50` typed into an
amount box from silently inverting a debt.

### `CorrectionDelta` — the arithmetic behind fixing a typo

```csharp
var corrected = recordedAmount < 0m ? -correctedMagnitude : correctedMagnitude;
return corrected - recordedAmount;
```

Three properties make this the whole of the correction feature:

1. **The shopkeeper types a magnitude, never a sign.** "It should have been 100"
   is a number they can produce; "+900" is not. The direction is taken from the
   entry being corrected, because a typo changes *how much* moved, never *which
   way*. This is the same contract as every other amount box in the app (D4).
2. **`recorded + delta` is exactly the corrected entry.** That identity is what
   the test asserts, rather than the specific delta values.
3. **A corrected magnitude of `0` is legal** and means "this never happened":
   the delta cancels the entry out precisely while leaving it on the record.
   Append-only has no delete, and the shopkeeper who logged a purchase twice
   needs an answer.

Two guards. A **negative** `correctedMagnitude` throws — direction is not the
typist's to enter. A `recordedAmount` of **zero** throws, because an entry of
zero has no direction to correct towards; no such entry can exist today
(`SignedAmount` rejects non-positive input and `RecordAdjustmentAsync` rejects
zero), so this is a guard against a future kind that forgets the rule.

Returning **zero** is not an error: it means the entry already reads as the
corrected amount. The caller must post nothing, because `RecordAdjustmentAsync`
rejects an adjustment of zero. `PartyStatementViewModel` turns that case into a
sentence rather than an exception — see
[04 §9](04-app-shell.md#the-correction-flow).

## 4. `LedgerService` — the use-case layer

[`LedgerService.cs`](../src/Oab.Core/Ledger/LedgerService.cs). A primary
constructor taking `ILedgerStore`. **Every method only ever appends.** UI modules
call this; they never construct `LedgerEntry` rows.

### `RecordPurchaseAsync`

```csharp
Task<Document> RecordPurchaseAsync(
    Guid supplierId, decimal amount, bool paidNow, DateTimeOffset occurredAt,
    string? note = null, IEnumerable<DocumentLine>? lines = null,
    string? documentNumber = null, CancellationToken ct = default)
```

Creates a `Document` of kind `Purchase`, attaches any lines (setting each
line's `DocumentId`), then appends:

- always: a `Purchase` entry for `-amount`;
- if `paidNow`: additionally a `PaymentOut` entry for `+amount`, same document,
  same timestamp, same note.

> **A cash purchase is simply a purchase that is already settled.** There is no
> "cash" entry kind, and no branch anywhere else in the system that has to know
> the difference.

Returns the created `Document`. Throws `ArgumentOutOfRangeException` via
`SignedAmount` if `amount <= 0`.

### `RecordSaleAsync`

Identical shape, mirrored: `DocumentKind.Sale`, a `Sale` entry for `+amount`,
and when `paidNow` a `PaymentIn` entry for `-amount`.

### `RecordPaymentOutAsync` / `RecordPaymentInAsync`

```csharp
Task<LedgerEntry> RecordPaymentOutAsync(
    Guid partyId, decimal amount, DateTimeOffset occurredAt,
    Guid? documentId = null, string? note = null, CancellationToken ct = default)
```

Appends a single entry. Passing `documentId` ties the payment to a specific
invoice so that invoice shows as (partially) paid; omitting it records a
free-standing payment against the party's overall balance. Both usages exist in
the product: "Pay remaining" on a purchase row passes the document; "Record
payment" on a supplier row does not.

### `RecordAdjustmentAsync`

```csharp
Task<LedgerEntry> RecordAdjustmentAsync(
    Guid partyId, decimal signedAmount, DateTimeOffset occurredAt,
    string note, Guid? documentId = null, CancellationToken ct = default)
```

The correction mechanism. Two guards, both enforced before anything is written:

- `signedAmount == 0` → `ArgumentOutOfRangeException` ("an adjustment of zero
  changes nothing");
- blank or whitespace `note` → `ArgumentException` ("adjustments must say why").

The amount is **pre-signed**: positive means the party owes the shop more. It is
the only method that takes an already-signed amount, and
`LedgerMath.CorrectionDelta` is what produces one.

**Called from** `PartyStatementViewModel.CorrectAsync` — tap an entry on the
party statement, say what it should have been, say why
([04 §9](04-app-shell.md#the-correction-flow)). The `documentId` argument is
passed the corrected entry's own `DocumentId`, so correcting a purchase also
corrects what that invoice still has outstanding.

### Queries

| Method | Returns |
|---|---|
| `GetPartyBalanceAsync(partyId)` | Delegates to the store. Signed. |
| `GetDocumentOutstandingAsync(documentId)` | `LedgerMath.Outstanding(entries for that document)` — positive, `0` = settled. |

## 5. Worked examples

### A credit purchase, partially then fully paid

| Step | Entries appended | Party balance | Document outstanding |
|---|---|---|---|
| `RecordPurchaseAsync(s, 250, paidNow: false)` | `Purchase −250` | **−250** (shop owes 250) | 250 |
| `RecordPaymentOutAsync(s, 100, doc)` | `PaymentOut +100` | **−150** | 150 |
| `RecordPaymentOutAsync(s, 150, doc)` | `PaymentOut +150` | **0** | 0 (settled) |

Pinned by `LedgerServiceTests.PartialPayment_ReducesDebt_DocumentStillOutstanding`
and `FullPaymentInInstallments_SettlesDocument`.

### A cash purchase

| Step | Entries appended | Balance | Row count |
|---|---|---|---|
| `RecordPurchaseAsync(s, 100, paidNow: true)` | `Purchase −100`, `PaymentOut +100` | **0** | 2 |

### The same person as supplier *and* customer

| Step | Entries | Balance |
|---|---|---|
| `RecordPurchaseAsync(p, 100, paidNow: false)` | `Purchase −100` | −100 |
| `RecordSaleAsync(p, 30, paidNow: false)` | `Sale +30` | **−70** |

One net number, no reconciliation, no second entity type. This is the payoff of
the single-`Party` decision.

### A typo, corrected

| Step | Entries | Balance | History |
|---|---|---|---|
| `RecordPurchaseAsync(s, 1000, paidNow: false)` | `Purchase −1000` | −1000 | 1 row |
| `RecordAdjustmentAsync(s, +900, "typo: was 1000")` | `Adjustment +900` | **−100** | **2 rows — the original is untouched** |

The `+900` is not typed by anyone: the shopkeeper says "it should have been 100"
and `LedgerMath.CorrectionDelta(-1000, 100)` produces it.

The statement screen renders the adjustment labelled "Correction", outlined in
gold, with its note visible. The mistake stays in the history, exactly as a
crossed-out line stays in a paper notebook.

### A purchase that never happened

| Step | Entries | Balance | Document outstanding |
|---|---|---|---|
| `RecordPurchaseAsync(s, 250, paidNow: false)` | `Purchase −250` | −250 | 250 |
| correct to **0** → `RecordAdjustmentAsync(s, +250, note, doc)` | `Adjustment +250` | **0** | **0** |

Passing the document id is what makes the second column and the third agree. Omit
it and the party balance is right while the purchases list still offers to pay
off 250 — the kind of disagreement that costs the shopkeeper's trust in one
sitting. Pinned by
`PartyStatementViewModelTests.CorrectionInheritsTheDocumentSoTheInvoiceStopsAskingForTheOldAmount`.

## 6. `MoneyFormat` — [`MoneyFormat.cs`](../src/Oab.Core/Formatting/MoneyFormat.cs)

```csharp
static string Format(decimal amount, CultureInfo culture,
                     string currencySymbol = "", bool useArabicIndicDigits = false)
```

Formats the **magnitude** of the amount — `Math.Abs` — with two decimals in the
given culture, optionally mapping ASCII digits to Arabic-Indic (`٠١٢٣٤٥٦٧٨٩`),
then appending the currency symbol separated by a space.

**The sign is intentionally dropped.** Callers phrase direction in words: "You
owe: 500.00 SP", not "−500.00 SP". A minus sign in front of a number in an RTL
layout is ambiguous at a glance; the word is not.

Digit shaping is done by hand (`MapDigits`) because .NET's number formatting
ignores `NumberFormatInfo.NativeDigits`. The decimal separator and grouping
separator stay whatever the culture says — only the digits are replaced.

| Input | Output |
|---|---|
| `1250m, en-US, "SP"` | `1,250.00 SP` |
| `-500m, en-US` | `500.00` |
| `123m, en-US, useArabicIndicDigits: true` | `١٢٣.٠٠` |
| `10m, en-US, "د.ج", useArabicIndicDigits: true` | `١٠.٠٠ د.ج` |

It lives in Core, not in the MAUI layer, so it can be unit-tested without
spinning up a runtime — [`MoneyFormatTests`](../tests/Oab.Core.Tests/MoneyFormatTests.cs).

## 7. `LedgerSummaryReport` — [`LedgerSummaryReport.cs`](../src/Oab.Core/Reporting/LedgerSummaryReport.cs)

Renders the entire book as plain text a shopkeeper can read in WhatsApp. This is
the human-readable half of a backup: if the phone, the app, and the `.db` file
are all gone, this is the copy a person can still act on.

```csharp
static string Build(string shopName, DateTimeOffset generatedAt,
                    IEnumerable<SummaryLine> lines, SummaryLabels labels,
                    CultureInfo culture, string currencySymbol = "",
                    bool useArabicIndicDigits = false)
```

- `SummaryLine(string PartyName, decimal Balance)` — one party's position.
- `SummaryLabels(string YouOwe, string OwesYou, string Settled, string Total)` —
  wording is **passed in**, not looked up, so the function stays pure and
  testable. The app layer supplies localized text.

Output structure:

```
<shop name>
<yyyy-MM-dd HH:mm, invariant culture>

== You owe ==
- Acme: 500.00 SP
- Beta: 120.00 SP
Total: 620.00 SP

== Owed to you ==
- Sami: 80.00 SP
Total: 80.00 SP

Settled: 1
```

Details that are load-bearing:

- Parties the shop owes are sorted **most-negative first**; parties who owe the
  shop are sorted **largest first**. The biggest number is at the top of each
  section, where it is read first.
- An empty section is omitted entirely rather than printed with a zero total.
- The `Settled:` line is a **count**, not an amount, and is formatted with plain
  culture formatting — never run through the money formatter.
- `'\n'` is appended explicitly rather than using `AppendLine`, so the file is
  byte-identical on Windows and Android. Pinned by
  `LedgerSummaryReportTests.LineEndingsAreStableAcrossPlatforms`.
- The timestamp uses `CultureInfo.InvariantCulture` so a backup made in Arabic
  is still parseable and sortable by a person or a script.

## 8. `ILedgerStore` — the persistence boundary

[`ILedgerStore.cs`](../src/Oab.Core/Ledger/ILedgerStore.cs). Implemented over
SQLite in `Oab.Data`, and by `InMemoryLedgerStore` in `tests/Oab.TestSupport`.

```csharp
Task AddPartyAsync(Party, ct);
Task UpdatePartyAsync(Party, ct);
Task<Party?> GetPartyAsync(Guid, ct);
Task<IReadOnlyList<Party>> GetPartiesAsync(bool includeArchived = false, PartyRole? role = null, ct);

Task AddDocumentAsync(Document, ct);
Task<Document?> GetDocumentAsync(Guid, ct);
Task<IReadOnlyList<Document>> GetDocumentsAsync(DocumentKind? kind = null, Guid? partyId = null, ct);

Task AddEntriesAsync(IReadOnlyList<LedgerEntry>, ct);
Task<IReadOnlyList<LedgerEntry>> GetEntriesForPartyAsync(Guid, ct);
Task<IReadOnlyList<LedgerEntry>> GetEntriesForDocumentAsync(Guid, ct);

Task<decimal> GetPartyBalanceAsync(Guid, ct);
Task<IReadOnlyDictionary<Guid, decimal>> GetBalancesAsync(ct);
```

> **Read that list again for what is missing: there is no `UpdateEntryAsync`
> and no `DeleteEntryAsync`.** The append-only rule is not a convention here, it
> is the shape of the interface. A future contributor cannot violate it by
> accident; they would have to widen the interface first, which is a visible act.

`AddEntriesAsync` takes a *list* rather than a single entry because a cash
purchase must write both of its entries in one transaction.

## 9. Invariants

| Invariant | Enforced by |
|---|---|
| Balances are always `SUM(entries)` | No balance field exists on `Party` |
| Entries are never modified or deleted | `ILedgerStore` has no such method |
| Non-adjustment amounts are positive at the boundary | `LedgerMath.SignedAmount` throws |
| Adjustments carry a reason | `LedgerService.RecordAdjustmentAsync` throws on blank note |
| Adjustments never pass through `SignedAmount` | `SignedAmount` throws for that kind |
| A correction's direction comes from the entry, not the typist | `CorrectionDelta` throws on a negative magnitude |
| A correction to a document also corrects that document | The adjustment inherits the entry's `DocumentId` |
| A cash purchase and a settled credit purchase are indistinguishable in the data | Both are `Purchase` + `PaymentOut` totalling zero |
| Money never becomes a `double` | `decimal` throughout, stored as TEXT in SQLite (see [03 §3](03-data-layer.md#3-why-decimals-are-stored-as-text)) |

## 10. Test coverage

[`tests/Oab.Core.Tests`](../tests/Oab.Core.Tests) — **42 tests, all passing.**

| File | Covers |
|---|---|
| `LedgerMathTests` | Sign convention per kind, adjustment rejection, non-positive rejection, `Outstanding` positivity for both document directions, empty-ledger behaviour, `CorrectionDelta` in both directions plus correct-to-zero, already-correct, and its two guards |
| `LedgerServiceTests` | Credit and cash purchases, partial and installment payments, sales, customer repayment, the same party as both roles, adjustments preserving history, adjustment validation, non-positive rejection, document lines, per-party balance map |
| `MoneyFormatTests` | Grouping, two decimals, symbol placement, sign dropping, Arabic-Indic shaping |
| `LedgerSummaryReportTests` | Section grouping, totals, omitted empty sections, empty book, platform-stable line endings, Arabic-Indic digits |

---

Next: [03 — Data Layer](03-data-layer.md)
