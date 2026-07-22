using Microsoft.EntityFrameworkCore;
using Oab.Core.Domain;
using Oab.Data;

namespace Oab.Data.Tests;

/// <summary>
/// Role tags separate the supplier and customer lists while keeping one Party
/// table. The legacy rule — untagged (None) parties appear in every list — is
/// what stops old databases from losing rows when the Roles column is added.
/// </summary>
public sealed class PartyRoleFilterTests : IDisposable
{
    private sealed class TestDbFactory(DbContextOptions<OabDbContext> options) : IDbContextFactory<OabDbContext>
    {
        public OabDbContext CreateDbContext() => new(options);
    }

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"oab-roles-{Guid.NewGuid():N}.db");
    private readonly LedgerStore _store;

    public PartyRoleFilterTests()
    {
        var options = new DbContextOptionsBuilder<OabDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        using (var db = new OabDbContext(options))
            db.Database.Migrate();
        _store = new LedgerStore(new TestDbFactory(options));
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(_dbPath);
    }

    [Fact]
    public async Task Filter_SeparatesSuppliersFromCustomers()
    {
        await _store.AddPartyAsync(new Party { Name = "Distributor", Roles = PartyRole.Supplier });
        await _store.AddPartyAsync(new Party { Name = "Walk-in buyer", Roles = PartyRole.Customer });

        var suppliers = await _store.GetPartiesAsync(role: PartyRole.Supplier);
        var customers = await _store.GetPartiesAsync(role: PartyRole.Customer);

        Assert.Equal("Distributor", Assert.Single(suppliers).Name);
        Assert.Equal("Walk-in buyer", Assert.Single(customers).Name);
    }

    [Fact]
    public async Task PartyWithBothRoles_AppearsInEitherList()
    {
        await _store.AddPartyAsync(new Party
        {
            Name = "Neighbor shop",
            Roles = PartyRole.Supplier | PartyRole.Customer,
        });

        Assert.Single(await _store.GetPartiesAsync(role: PartyRole.Supplier));
        Assert.Single(await _store.GetPartiesAsync(role: PartyRole.Customer));
    }

    [Fact]
    public async Task UntaggedLegacyParty_ShowsInEveryList()
    {
        // Roles defaults to None — mirrors a row created before roles existed.
        await _store.AddPartyAsync(new Party { Name = "Old contact" });

        Assert.Single(await _store.GetPartiesAsync(role: PartyRole.Supplier));
        Assert.Single(await _store.GetPartiesAsync(role: PartyRole.Customer));
        Assert.Single(await _store.GetPartiesAsync()); // no filter
    }
}
