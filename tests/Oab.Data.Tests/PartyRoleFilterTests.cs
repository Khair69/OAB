using Oab.Core.Domain;

namespace Oab.Data.Tests;

/// <summary>
/// Role tags separate the supplier and customer lists while keeping one Party
/// table. The legacy rule — untagged (None) parties appear in every list — is
/// what stops old databases from losing rows when the Roles column is added.
/// </summary>
public sealed class PartyRoleFilterTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new("oab-roles");

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Filter_SeparatesSuppliersFromCustomers()
    {
        await _db.Store.AddPartyAsync(new Party { Name = "Distributor", Roles = PartyRole.Supplier });
        await _db.Store.AddPartyAsync(new Party { Name = "Walk-in buyer", Roles = PartyRole.Customer });

        var suppliers = await _db.Store.GetPartiesAsync(role: PartyRole.Supplier);
        var customers = await _db.Store.GetPartiesAsync(role: PartyRole.Customer);

        Assert.Equal("Distributor", Assert.Single(suppliers).Name);
        Assert.Equal("Walk-in buyer", Assert.Single(customers).Name);
    }

    [Fact]
    public async Task PartyWithBothRoles_AppearsInEitherList()
    {
        await _db.Store.AddPartyAsync(new Party
        {
            Name = "Neighbor shop",
            Roles = PartyRole.Supplier | PartyRole.Customer,
        });

        Assert.Single(await _db.Store.GetPartiesAsync(role: PartyRole.Supplier));
        Assert.Single(await _db.Store.GetPartiesAsync(role: PartyRole.Customer));
    }

    [Fact]
    public async Task UntaggedLegacyParty_ShowsInEveryList()
    {
        // Roles defaults to None — mirrors a row created before roles existed.
        await _db.Store.AddPartyAsync(new Party { Name = "Old contact" });

        Assert.Single(await _db.Store.GetPartiesAsync(role: PartyRole.Supplier));
        Assert.Single(await _db.Store.GetPartiesAsync(role: PartyRole.Customer));
        Assert.Single(await _db.Store.GetPartiesAsync()); // no filter
    }
}
