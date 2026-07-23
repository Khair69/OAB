using Oab.Core.Domain;
using Oab.Core.Ledger;

namespace Oab.Data.Tests;

/// <summary>
/// The two document-scoped reads, against real SQLite:
/// <c>GetDocumentAsync</c> (one invoice with its lines) and
/// <c>GetEntriesForDocumentAsync</c> (the money that references it).
///
/// <para>
/// The second one is what answers "is this invoice paid?" on the purchases list
/// and what <c>LedgerService.GetDocumentOutstandingAsync</c> is built on, so it
/// is on the screen a shopkeeper looks at most — and it had no test against a
/// real database, which is the exact profile of the two queries that turned out
/// to be throwing on every open.
/// </para>
/// </summary>
public sealed class DocumentLookupSqliteTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new("oab-documents");
    private static readonly DateTimeOffset Now = new(2026, 7, 23, 10, 0, 0, TimeSpan.FromHours(3));

    public void Dispose() => _db.Dispose();

    private async Task<Party> AddSupplierAsync(string name = "Distributor")
    {
        var supplier = new Party { Name = name, Roles = PartyRole.Supplier };
        await _db.Store.AddPartyAsync(supplier);
        return supplier;
    }

    // ---- GetDocumentAsync -------------------------------------------------

    [Fact]
    public async Task Document_ComesBackWithEveryLine()
    {
        var supplier = await AddSupplierAsync();
        var doc = await _db.Ledger.RecordPurchaseAsync(supplier.Id, 400m, paidNow: false, Now, lines:
        [
            new DocumentLine { Description = "Shampoo 400ml", Quantity = 25, UnitPrice = 10m },
            new DocumentLine { Description = "Soap bar", Quantity = 50, UnitPrice = 2m },
            new DocumentLine { Description = "Delivery", Quantity = 1, UnitPrice = 50m },
        ]);

        var saved = await _db.Store.GetDocumentAsync(doc.Id);

        Assert.NotNull(saved);
        Assert.Equal(3, saved.Lines.Count);
        Assert.Equal(400m, saved.Lines.Sum(l => l.Total));
        Assert.Contains(saved.Lines, l => l.Description == "Soap bar" && l.Quantity == 50);
    }

    /// <summary>
    /// Fractional quantities and prices are the normal case in a souk — half a
    /// kilo, 12.5 per unit. If either decayed to double on the way to SQLite the
    /// line total would drift away from the invoice, which is D6's whole point
    /// applied to a column nothing had exercised.
    /// </summary>
    [Fact]
    public async Task DocumentLines_KeepDecimalQuantitiesAndPrices_Exactly()
    {
        var supplier = await AddSupplierAsync();
        var doc = await _db.Ledger.RecordPurchaseAsync(supplier.Id, 3.75m, paidNow: false, Now, lines:
        [
            new DocumentLine { Description = "Olives", Quantity = 0.5m, UnitPrice = 7.5m },
        ]);

        var line = Assert.Single((await _db.Store.GetDocumentAsync(doc.Id))!.Lines);

        Assert.Equal(0.5m, line.Quantity);
        Assert.Equal(7.5m, line.UnitPrice);
        Assert.Equal(3.75m, line.Total);
    }

    /// <summary>
    /// <c>Number</c> is the supplier's own invoice number — the string a
    /// shopkeeper would use to find a paper copy. Persisted, never yet displayed.
    /// </summary>
    [Fact]
    public async Task Document_KeepsItsNumberNoteAndOffset()
    {
        var supplier = await AddSupplierAsync();
        var doc = await _db.Ledger.RecordPurchaseAsync(
            supplier.Id, 100m, paidNow: false, Now,
            note: "crate of shampoo", documentNumber: "INV-2026-0412");

        var saved = await _db.Store.GetDocumentAsync(doc.Id);

        Assert.Equal("INV-2026-0412", saved!.Number);
        Assert.Equal("crate of shampoo", saved.Note);
        Assert.Equal(DocumentKind.Purchase, saved.Kind);
        // Same trap as the ledger entries: the offset is part of the value, or a
        // backdated invoice lands on the wrong day.
        Assert.Equal(TimeSpan.FromHours(3), saved.OccurredAt.Offset);
        Assert.Equal(Now, saved.OccurredAt);
    }

    /// <summary>
    /// A document with no lines is the ordinary case — most shops record money
    /// only. It must come back with an empty list, not null and not a throw.
    /// </summary>
    [Fact]
    public async Task Document_WithNoLines_ComesBackEmpty_NotNull()
    {
        var supplier = await AddSupplierAsync();
        var doc = await _db.Ledger.RecordPurchaseAsync(supplier.Id, 100m, paidNow: false, Now);

        var saved = await _db.Store.GetDocumentAsync(doc.Id);

        Assert.NotNull(saved);
        Assert.Empty(saved.Lines);
    }

    [Fact]
    public async Task UnknownDocument_IsNull_NotAThrow() =>
        Assert.Null(await _db.Store.GetDocumentAsync(Guid.NewGuid()));

    /// <summary>
    /// Lines belong to exactly one invoice. Getting this wrong duplicates a
    /// crate of shampoo onto someone else's bill.
    /// </summary>
    [Fact]
    public async Task Document_DoesNotPickUpAnotherDocumentsLines()
    {
        var supplier = await AddSupplierAsync();
        var first = await _db.Ledger.RecordPurchaseAsync(supplier.Id, 10m, paidNow: false, Now, lines:
            [new DocumentLine { Description = "Mine", Quantity = 1, UnitPrice = 10m }]);
        await _db.Ledger.RecordPurchaseAsync(supplier.Id, 20m, paidNow: false, Now, lines:
            [new DocumentLine { Description = "Theirs", Quantity = 1, UnitPrice = 20m }]);

        var saved = await _db.Store.GetDocumentAsync(first.Id);

        Assert.Equal("Mine", Assert.Single(saved!.Lines).Description);
    }

    // ---- GetEntriesForDocumentAsync ---------------------------------------

    /// <summary>
    /// The list-screen question, end to end: a credit purchase, then a part
    /// payment against that invoice, and the remainder must fall accordingly.
    /// </summary>
    [Fact]
    public async Task DocumentEntries_DriveTheOutstandingRemainder()
    {
        var supplier = await AddSupplierAsync();
        var doc = await _db.Ledger.RecordPurchaseAsync(supplier.Id, 250m, paidNow: false, Now);
        Assert.Equal(250m, await _db.Ledger.GetDocumentOutstandingAsync(doc.Id));

        await _db.Ledger.RecordPaymentOutAsync(supplier.Id, 100m, Now.AddDays(1), doc.Id);
        Assert.Equal(150m, await _db.Ledger.GetDocumentOutstandingAsync(doc.Id));

        await _db.Ledger.RecordPaymentOutAsync(supplier.Id, 150m, Now.AddDays(2), doc.Id);
        Assert.Equal(0m, await _db.Ledger.GetDocumentOutstandingAsync(doc.Id));
        Assert.True(LedgerMath.IsSettled(await _db.Store.GetEntriesForDocumentAsync(doc.Id)));
    }

    /// <summary>
    /// A payment the shopkeeper made without picking an invoice ("here's 50 off
    /// what I owe you") carries no <c>DocumentId</c>. It moves the party's
    /// balance and must leave every invoice's remainder alone — otherwise a
    /// generic payment would silently mark a specific bill as paid.
    /// </summary>
    [Fact]
    public async Task DocumentEntries_IgnoreStandalonePaymentsAndOtherInvoices()
    {
        var supplier = await AddSupplierAsync();
        var mine = await _db.Ledger.RecordPurchaseAsync(supplier.Id, 100m, paidNow: false, Now);
        var other = await _db.Ledger.RecordPurchaseAsync(supplier.Id, 70m, paidNow: false, Now);
        await _db.Ledger.RecordPaymentOutAsync(supplier.Id, 50m, Now.AddDays(1)); // no document
        await _db.Ledger.RecordPaymentOutAsync(supplier.Id, 70m, Now.AddDays(1), other.Id);

        var entries = await _db.Store.GetEntriesForDocumentAsync(mine.Id);

        Assert.Equal(EntryKind.Purchase, Assert.Single(entries).Kind);
        Assert.Equal(100m, await _db.Ledger.GetDocumentOutstandingAsync(mine.Id));
        // ...while the party as a whole is 100 + 70 - 50 - 70 = 50 in the hole.
        Assert.Equal(-50m, await _db.Store.GetPartyBalanceAsync(supplier.Id));
    }

    /// <summary>
    /// The correction flow's promise, against a real database: correcting a
    /// mistyped invoice posts an <c>Adjustment</c> carrying the entry's
    /// <c>DocumentId</c>, so the invoice's remainder follows the fix instead of
    /// only the party's balance. Everything asserted here has been claimed by
    /// the docs since the flow shipped and only ever tested in memory.
    /// </summary>
    [Fact]
    public async Task ACorrection_MovesTheInvoicesRemainder_NotJustThePartyBalance()
    {
        var supplier = await AddSupplierAsync();
        // Fat finger: 2,500 typed for a 250 crate.
        var doc = await _db.Ledger.RecordPurchaseAsync(supplier.Id, 2500m, paidNow: false, Now);
        var wrong = Assert.Single(await _db.Store.GetEntriesForDocumentAsync(doc.Id));

        var delta = LedgerMath.CorrectionDelta(wrong.Amount, 250m);
        await _db.Ledger.RecordAdjustmentAsync(supplier.Id, delta, Now, "Was 2,500.00 SP", doc.Id);

        Assert.Equal(250m, await _db.Ledger.GetDocumentOutstandingAsync(doc.Id));
        Assert.Equal(-250m, await _db.Store.GetPartyBalanceAsync(supplier.Id));
        // Nothing was edited or removed: the wrong number is still on the record.
        var entries = await _db.Store.GetEntriesForDocumentAsync(doc.Id);
        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, e => e.Amount == -2500m);
        Assert.Contains(entries, e => e.Kind == EntryKind.Adjustment && e.Note == "Was 2,500.00 SP");
    }

    /// <summary>
    /// "This never happened" — a correction to zero cancels the invoice out
    /// exactly, and the entry that was wrong stays visible in the statement.
    /// </summary>
    [Fact]
    public async Task CorrectingToZero_SettlesTheInvoice_WithoutDeletingAnything()
    {
        var supplier = await AddSupplierAsync();
        var doc = await _db.Ledger.RecordPurchaseAsync(supplier.Id, 300m, paidNow: false, Now);
        var wrong = Assert.Single(await _db.Store.GetEntriesForDocumentAsync(doc.Id));

        await _db.Ledger.RecordAdjustmentAsync(
            supplier.Id, LedgerMath.CorrectionDelta(wrong.Amount, 0m), Now, "Never delivered", doc.Id);

        Assert.Equal(0m, await _db.Ledger.GetDocumentOutstandingAsync(doc.Id));
        Assert.Equal(0m, await _db.Store.GetPartyBalanceAsync(supplier.Id));
        Assert.Equal(2, (await _db.Store.GetEntriesForPartyAsync(supplier.Id)).Count);
    }

    /// <summary>
    /// A cash purchase posts both entries at once and is settled on arrival —
    /// the "paid now is just a purchase already paid" claim, at the storage layer.
    /// </summary>
    [Fact]
    public async Task ACashPurchase_IsSettledTheMomentItIsRecorded()
    {
        var supplier = await AddSupplierAsync();
        var doc = await _db.Ledger.RecordPurchaseAsync(supplier.Id, 80m, paidNow: true, Now);

        var entries = await _db.Store.GetEntriesForDocumentAsync(doc.Id);

        Assert.Equal(2, entries.Count);
        Assert.Equal(0m, await _db.Ledger.GetDocumentOutstandingAsync(doc.Id));
        Assert.Equal(0m, await _db.Store.GetPartyBalanceAsync(supplier.Id));
    }

    [Fact]
    public async Task EntriesForAnUnknownDocument_AreEmpty_NotAThrow() =>
        Assert.Empty(await _db.Store.GetEntriesForDocumentAsync(Guid.NewGuid()));

    // ---- GetEntriesForDocumentsAsync (the batched form) --------------------

    /// <summary>
    /// The batched read the purchases list now uses. It must agree, invoice for
    /// invoice, with asking one at a time — that is the only thing that makes it
    /// a safe replacement rather than a faster wrong answer.
    /// </summary>
    [Fact]
    public async Task BatchedEntries_MatchAskingOneDocumentAtATime()
    {
        var supplier = await AddSupplierAsync();
        var credit = await _db.Ledger.RecordPurchaseAsync(supplier.Id, 250m, paidNow: false, Now);
        var cash = await _db.Ledger.RecordPurchaseAsync(supplier.Id, 80m, paidNow: true, Now.AddDays(-1));
        var partPaid = await _db.Ledger.RecordPurchaseAsync(supplier.Id, 100m, paidNow: false, Now.AddDays(-2));
        await _db.Ledger.RecordPaymentOutAsync(supplier.Id, 40m, Now, partPaid.Id);
        await _db.Ledger.RecordPaymentOutAsync(supplier.Id, 15m, Now); // unattached

        var ids = new[] { credit.Id, cash.Id, partPaid.Id };
        var batched = await _db.Store.GetEntriesForDocumentsAsync(ids);

        foreach (var id in ids)
        {
            var single = await _db.Store.GetEntriesForDocumentAsync(id);
            Assert.Equal(
                single.Select(e => e.Id).OrderBy(x => x),
                batched[id].Select(e => e.Id).OrderBy(x => x));
        }
        Assert.Equal(250m, LedgerMath.Outstanding(batched[credit.Id]));
        Assert.Equal(0m, LedgerMath.Outstanding(batched[cash.Id]));
        Assert.Equal(60m, LedgerMath.Outstanding(batched[partPaid.Id]));
    }

    /// <summary>
    /// A document nobody has posted money against is absent from the dictionary
    /// rather than present-and-empty, so the list screen has to handle the miss.
    /// It does, with <c>GetValueOrDefault(id, [])</c>; this pins the contract.
    /// </summary>
    [Fact]
    public async Task BatchedEntries_OmitDocumentsWithNothingAgainstThem()
    {
        var supplier = await AddSupplierAsync();
        var real = await _db.Ledger.RecordPurchaseAsync(supplier.Id, 10m, paidNow: false, Now);
        var stranger = Guid.NewGuid();

        var batched = await _db.Store.GetEntriesForDocumentsAsync([real.Id, stranger]);

        Assert.True(batched.ContainsKey(real.Id));
        Assert.False(batched.ContainsKey(stranger));
        Assert.Empty(batched.GetValueOrDefault(stranger, []));
    }

    [Fact]
    public async Task BatchedEntries_ForNoDocuments_IsEmpty_AndAsksNothing() =>
        Assert.Empty(await _db.Store.GetEntriesForDocumentsAsync([]));

    /// <summary>
    /// A shop two years in has thousands of invoices, and the obvious way to
    /// write this query — a bare <c>ids.Contains(...)</c> — compiles to one SQL
    /// parameter per id. That is a ceiling with a shop's growth pointed at it, so
    /// the store forces the single-JSON-parameter form with <c>EF.Parameter</c>.
    /// 1,500 is past SQLite's historical 999-parameter limit; if the store ever
    /// loses that call, this is what says so.
    /// </summary>
    [Fact]
    public async Task BatchedEntries_HandleMoreDocumentsThanSqliteAllowsParameters()
    {
        const int count = 1_500;
        var supplier = await AddSupplierAsync();

        // Seeded through one context: the store's one-at-a-time writes are the
        // right shape for a shopkeeper and the wrong shape for 1,500 rows.
        var documents = new List<Document>(count);
        var entries = new List<LedgerEntry>(count);
        for (var i = 0; i < count; i++)
        {
            var doc = new Document
            {
                PartyId = supplier.Id,
                Kind = DocumentKind.Purchase,
                OccurredAt = Now.AddDays(-i),
            };
            documents.Add(doc);
            entries.Add(new LedgerEntry
            {
                PartyId = supplier.Id,
                DocumentId = doc.Id,
                OccurredAt = doc.OccurredAt,
                Kind = EntryKind.Purchase,
                Amount = -10m,
            });
        }
        await using (var db = _db.CreateDbContext())
        {
            db.Documents.AddRange(documents);
            db.LedgerEntries.AddRange(entries);
            await db.SaveChangesAsync();
        }

        var batched = await _db.Store.GetEntriesForDocumentsAsync([.. documents.Select(d => d.Id)]);

        Assert.Equal(count, batched.Count);
        Assert.All(batched.Values, e => Assert.Equal(10m, LedgerMath.Outstanding(e)));
    }

    // ---- GetDocumentsAsync filters ----------------------------------------

    /// <summary>
    /// The purchases-list filter, which is the only <c>GetDocumentsAsync</c>
    /// overload with a caller. The <c>partyId</c> filter has none — and one is
    /// coming, because the party statement will want this shop's invoices for
    /// one supplier.
    /// </summary>
    [Fact]
    public async Task Documents_CanBeFilteredByParty_AndByKind()
    {
        var a = await AddSupplierAsync("Supplier A");
        var b = await AddSupplierAsync("Supplier B");
        await _db.Ledger.RecordPurchaseAsync(a.Id, 10m, paidNow: false, Now);
        await _db.Ledger.RecordPurchaseAsync(a.Id, 20m, paidNow: false, Now.AddDays(-1));
        await _db.Ledger.RecordPurchaseAsync(b.Id, 30m, paidNow: false, Now);
        await _db.Ledger.RecordSaleAsync(b.Id, 40m, paidNow: false, Now);

        Assert.Equal(2, (await _db.Store.GetDocumentsAsync(partyId: a.Id)).Count);
        Assert.Equal(2, (await _db.Store.GetDocumentsAsync(partyId: b.Id)).Count);
        Assert.Equal(3, (await _db.Store.GetDocumentsAsync(DocumentKind.Purchase)).Count);
        Assert.Single(await _db.Store.GetDocumentsAsync(DocumentKind.Purchase, b.Id));
        Assert.Equal(4, (await _db.Store.GetDocumentsAsync()).Count);
    }
}
