using Microsoft.EntityFrameworkCore;
using Oab.Core.Domain;
using Oab.Core.Ledger;
using Oab.Data;

namespace Oab.Data.Tests;

/// <summary>
/// End-to-end: LedgerService -> LedgerStore -> EF Core -> real SQLite file,
/// including running the actual migrations. This is the offline-durability
/// guarantee the whole product rests on.
/// </summary>
public sealed class LedgerStoreSqliteTests : IDisposable
{
    private sealed class TestDbFactory(DbContextOptions<OabDbContext> options) : IDbContextFactory<OabDbContext>
    {
        public OabDbContext CreateDbContext() => new(options);
    }

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"oab-test-{Guid.NewGuid():N}.db");
    private readonly LedgerStore _store;
    private readonly LedgerService _service;
    private static readonly DateTimeOffset Now = new(2026, 7, 8, 10, 0, 0, TimeSpan.FromHours(3));

    public LedgerStoreSqliteTests()
    {
        var options = new DbContextOptionsBuilder<OabDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        using (var db = new OabDbContext(options))
        {
            db.Database.Migrate();
        }
        _store = new LedgerStore(new TestDbFactory(options));
        _service = new LedgerService(_store);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(_dbPath);
    }

    [Fact]
    public async Task FullFlow_PersistsAndBalances()
    {
        var supplier = new Party { Name = "Distributor A", Phone = "0555" };
        await _store.AddPartyAsync(supplier);

        var doc = await _service.RecordPurchaseAsync(supplier.Id, 250m, paidNow: false, Now,
            note: "shampoo crate", lines:
            [
                new DocumentLine { Description = "Shampoo 400ml", Quantity = 25, UnitPrice = 10m },
            ]);
        await _service.RecordPaymentOutAsync(supplier.Id, 100m, Now.AddDays(2), doc.Id);

        Assert.Equal(-150m, await _service.GetPartyBalanceAsync(supplier.Id));
        Assert.Equal(150m, await _service.GetDocumentOutstandingAsync(doc.Id));

        var savedDoc = await _store.GetDocumentAsync(doc.Id);
        Assert.NotNull(savedDoc);
        Assert.Single(savedDoc.Lines);
        Assert.Equal(250m, savedDoc.Lines[0].Total);

        var balances = await _store.GetBalancesAsync();
        Assert.Equal(-150m, balances[supplier.Id]);
    }

    [Fact]
    public async Task DecimalAmounts_SurviveRoundTrip_Exactly()
    {
        var party = new Party { Name = "P" };
        await _store.AddPartyAsync(party);
        await _service.RecordSaleAsync(party.Id, 0.1m, paidNow: false, Now);
        await _service.RecordSaleAsync(party.Id, 0.2m, paidNow: false, Now);

        // decimal must not decay to double in storage: 0.1 + 0.2 == exactly 0.3
        Assert.Equal(0.3m, await _service.GetPartyBalanceAsync(party.Id));
    }

    [Fact]
    public async Task Parties_ArchivedAreHiddenByDefault()
    {
        await _store.AddPartyAsync(new Party { Name = "Active" });
        var archived = new Party { Name = "Old supplier", IsArchived = true };
        await _store.AddPartyAsync(archived);

        Assert.Single(await _store.GetPartiesAsync());
        Assert.Equal(2, (await _store.GetPartiesAsync(includeArchived: true)).Count);
    }

    [Fact]
    public async Task Reopen_DatabaseFile_DataSurvives()
    {
        var party = new Party { Name = "Persistent" };
        await _store.AddPartyAsync(party);
        await _service.RecordPurchaseAsync(party.Id, 42m, paidNow: false, Now);

        // Simulate app restart: brand-new context/factory over the same file.
        var reopenedOptions = new DbContextOptionsBuilder<OabDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        var reopenedStore = new LedgerStore(new TestDbFactory(reopenedOptions));

        Assert.Equal(-42m, await reopenedStore.GetPartyBalanceAsync(party.Id));
        Assert.Equal("Persistent", (await reopenedStore.GetPartyAsync(party.Id))?.Name);
    }
}
