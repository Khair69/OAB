using Oab.Core.Domain;

namespace Oab.Data.Tests;

/// <summary>
/// End-to-end: LedgerService -> LedgerStore -> EF Core -> real SQLite file,
/// including running the actual migrations. This is the offline-durability
/// guarantee the whole product rests on.
/// </summary>
public sealed class LedgerStoreSqliteTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new("oab-ledger");
    private static readonly DateTimeOffset Now = new(2026, 7, 8, 10, 0, 0, TimeSpan.FromHours(3));

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task FullFlow_PersistsAndBalances()
    {
        var supplier = new Party { Name = "Distributor A", Phone = "0555" };
        await _db.Store.AddPartyAsync(supplier);

        var doc = await _db.Ledger.RecordPurchaseAsync(supplier.Id, 250m, paidNow: false, Now,
            note: "shampoo crate", lines:
            [
                new DocumentLine { Description = "Shampoo 400ml", Quantity = 25, UnitPrice = 10m },
            ]);
        await _db.Ledger.RecordPaymentOutAsync(supplier.Id, 100m, Now.AddDays(2), doc.Id);

        Assert.Equal(-150m, await _db.Ledger.GetPartyBalanceAsync(supplier.Id));
        Assert.Equal(150m, await _db.Ledger.GetDocumentOutstandingAsync(doc.Id));

        var savedDoc = await _db.Store.GetDocumentAsync(doc.Id);
        Assert.NotNull(savedDoc);
        Assert.Single(savedDoc.Lines);
        Assert.Equal(250m, savedDoc.Lines[0].Total);

        var balances = await _db.Store.GetBalancesAsync();
        Assert.Equal(-150m, balances[supplier.Id]);
    }

    [Fact]
    public async Task DecimalAmounts_SurviveRoundTrip_Exactly()
    {
        var party = new Party { Name = "P" };
        await _db.Store.AddPartyAsync(party);
        await _db.Ledger.RecordSaleAsync(party.Id, 0.1m, paidNow: false, Now);
        await _db.Ledger.RecordSaleAsync(party.Id, 0.2m, paidNow: false, Now);

        // decimal must not decay to double in storage: 0.1 + 0.2 == exactly 0.3
        Assert.Equal(0.3m, await _db.Ledger.GetPartyBalanceAsync(party.Id));
    }

    [Fact]
    public async Task Parties_ArchivedAreHiddenByDefault()
    {
        await _db.Store.AddPartyAsync(new Party { Name = "Active" });
        var archived = new Party { Name = "Old supplier", IsArchived = true };
        await _db.Store.AddPartyAsync(archived);

        Assert.Single(await _db.Store.GetPartiesAsync());
        Assert.Equal(2, (await _db.Store.GetPartiesAsync(includeArchived: true)).Count);
    }

    /// <summary>
    /// This is what the purchases list calls on every <c>OnAppearing</c>, and
    /// until the global exception handler caught it on a real launch, nothing
    /// here ever called it against real SQLite — only against
    /// <c>InMemoryLedgerStore</c>, which sorts in C# and so cannot fail this way.
    /// <para>
    /// SQLite has no <c>DateTimeOffset</c>, so an <c>ORDER BY</c> on one is
    /// rejected at query-translation time: the screen threw
    /// <c>NotSupportedException</c> every single time it opened.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Documents_ComeBackNewestFirst()
    {
        var supplier = new Party { Name = "Distributor A", Roles = PartyRole.Supplier };
        await _db.Store.AddPartyAsync(supplier);
        await _db.Ledger.RecordPurchaseAsync(supplier.Id, 10m, paidNow: false, Now.AddDays(-2));
        await _db.Ledger.RecordPurchaseAsync(supplier.Id, 20m, paidNow: false, Now);
        await _db.Ledger.RecordPurchaseAsync(supplier.Id, 30m, paidNow: false, Now.AddDays(-1));

        var documents = await _db.Store.GetDocumentsAsync(DocumentKind.Purchase);

        Assert.Equal([Now, Now.AddDays(-1), Now.AddDays(-2)], documents.Select(d => d.OccurredAt));
    }

    /// <summary>The same trap, on the query behind the party statement.</summary>
    [Fact]
    public async Task PartyEntries_ComeBackNewestFirst()
    {
        var party = new Party { Name = "Sami", Roles = PartyRole.Customer };
        await _db.Store.AddPartyAsync(party);
        await _db.Ledger.RecordSaleAsync(party.Id, 10m, paidNow: false, Now.AddDays(-2));
        await _db.Ledger.RecordSaleAsync(party.Id, 20m, paidNow: false, Now);
        await _db.Ledger.RecordSaleAsync(party.Id, 30m, paidNow: false, Now.AddDays(-1));

        var entries = await _db.Store.GetEntriesForPartyAsync(party.Id);

        Assert.Equal([Now, Now.AddDays(-1), Now.AddDays(-2)], entries.Select(e => e.OccurredAt));
    }

    /// <summary>
    /// The offset is part of the value, not decoration: a purchase logged at
    /// 10:00+03:00 must not come back as 07:00 UTC, or a backdated entry lands on
    /// the wrong day in the statement.
    /// </summary>
    [Fact]
    public async Task OccurredAt_KeepsItsUtcOffset_AcrossStorage()
    {
        var party = new Party { Name = "P" };
        await _db.Store.AddPartyAsync(party);
        await _db.Ledger.RecordSaleAsync(party.Id, 10m, paidNow: false, Now);

        var entry = Assert.Single(await _db.Store.GetEntriesForPartyAsync(party.Id));

        Assert.Equal(TimeSpan.FromHours(3), entry.OccurredAt.Offset);
        Assert.Equal(Now, entry.OccurredAt);
    }

    [Fact]
    public async Task Reopen_DatabaseFile_DataSurvives()
    {
        var party = new Party { Name = "Persistent" };
        await _db.Store.AddPartyAsync(party);
        await _db.Ledger.RecordPurchaseAsync(party.Id, 42m, paidNow: false, Now);

        // Simulate app restart: brand-new context/factory over the same file.
        var reopenedStore = _db.ReopenStore();

        Assert.Equal(-42m, await reopenedStore.GetPartyBalanceAsync(party.Id));
        Assert.Equal("Persistent", (await reopenedStore.GetPartyAsync(party.Id))?.Name);
    }
}
