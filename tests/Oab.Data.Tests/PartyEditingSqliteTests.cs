using Microsoft.EntityFrameworkCore;
using Oab.Core.Domain;

namespace Oab.Data.Tests;

/// <summary>
/// <c>LedgerStore.UpdatePartyAsync</c> against real SQLite.
///
/// <para>
/// It had neither a caller nor a test, which by this project's standing rule
/// means it was a guess that compiled. It is also the method a future
/// "edit party" screen will be built on — the screen is cheap precisely because
/// this half is supposed to already work, so it had better actually work.
/// </para>
/// <para>
/// The thing being protected here is that editing a party never touches money.
/// A party's row is the one mutable thing in an append-only design; every test
/// below therefore checks the ledger is untouched as well as the edit landing.
/// </para>
/// </summary>
public sealed class PartyEditingSqliteTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new("oab-party-edit");
    private static readonly DateTimeOffset Now = new(2026, 7, 23, 10, 0, 0, TimeSpan.FromHours(3));

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Editing_APartysDetails_RoundTrips()
    {
        var party = new Party { Name = "Abu Ali", Roles = PartyRole.Supplier };
        await _db.Store.AddPartyAsync(party);

        var loaded = await _db.Store.GetPartyAsync(party.Id);
        loaded!.Name = "Abu Ali Distribution";
        loaded.Phone = "0955 123 456";
        loaded.Note = "Delivers Tuesdays";
        await _db.Store.UpdatePartyAsync(loaded);

        var reread = await _db.Store.GetPartyAsync(party.Id);
        Assert.Equal("Abu Ali Distribution", reread!.Name);
        Assert.Equal("0955 123 456", reread.Phone);
        Assert.Equal("Delivers Tuesdays", reread.Note);
    }

    /// <summary>
    /// <c>GetPartyAsync</c> hands back a detached (no-tracking) instance, so the
    /// update runs on an entity EF has never seen. That is the shape every
    /// caller will have, and the shape most likely to insert a duplicate instead
    /// of updating.
    /// </summary>
    [Fact]
    public async Task Editing_DoesNotLeaveASecondCopyOfTheParty()
    {
        var party = new Party { Name = "Sami" };
        await _db.Store.AddPartyAsync(party);

        var loaded = await _db.Store.GetPartyAsync(party.Id);
        loaded!.Phone = "0912";
        await _db.Store.UpdatePartyAsync(loaded);

        var all = await _db.Store.GetPartiesAsync(includeArchived: true);
        Assert.Equal(party.Id, Assert.Single(all).Id);
    }

    /// <summary>
    /// Flags round-trip as an int column. A party that is both supplier and
    /// customer — the souk's normal case, and the reason there is one Party type
    /// — must not come back as one or neither.
    /// </summary>
    [Fact]
    public async Task Editing_Roles_KeepsBothFlags()
    {
        var party = new Party { Name = "Neighbour shop", Roles = PartyRole.Supplier };
        await _db.Store.AddPartyAsync(party);

        var loaded = await _db.Store.GetPartyAsync(party.Id);
        loaded!.Roles |= PartyRole.Customer;
        await _db.Store.UpdatePartyAsync(loaded);

        var reread = await _db.Store.GetPartyAsync(party.Id);
        Assert.Equal(PartyRole.Supplier | PartyRole.Customer, reread!.Roles);
        Assert.Single(await _db.Store.GetPartiesAsync(role: PartyRole.Supplier));
        Assert.Single(await _db.Store.GetPartiesAsync(role: PartyRole.Customer));
    }

    /// <summary>
    /// Archiving is the closest thing to deletion the app has, and the whole
    /// point is that it is not one: the party leaves the list, the money stays.
    /// </summary>
    [Fact]
    public async Task Archiving_HidesTheParty_ButKeepsTheBalanceAndHistory()
    {
        var supplier = new Party { Name = "Old distributor", Roles = PartyRole.Supplier };
        await _db.Store.AddPartyAsync(supplier);
        await _db.Ledger.RecordPurchaseAsync(supplier.Id, 250m, paidNow: false, Now);
        await _db.Ledger.RecordPaymentOutAsync(supplier.Id, 100m, Now.AddDays(1));

        var loaded = await _db.Store.GetPartyAsync(supplier.Id);
        loaded!.IsArchived = true;
        await _db.Store.UpdatePartyAsync(loaded);

        Assert.Empty(await _db.Store.GetPartiesAsync());
        Assert.Empty(await _db.Store.GetPartiesAsync(role: PartyRole.Supplier));
        Assert.Single(await _db.Store.GetPartiesAsync(includeArchived: true));

        // The book is untouched: still owed 150, still two entries explaining it.
        Assert.Equal(-150m, await _db.Store.GetPartyBalanceAsync(supplier.Id));
        Assert.Equal(2, (await _db.Store.GetEntriesForPartyAsync(supplier.Id)).Count);
    }

    /// <summary>
    /// Un-archiving has to work too, or "archive" is a one-way door the
    /// shopkeeper can walk through by accident.
    /// </summary>
    [Fact]
    public async Task Archiving_IsReversible()
    {
        var party = new Party { Name = "Seasonal supplier", IsArchived = true };
        await _db.Store.AddPartyAsync(party);

        var loaded = Assert.Single(await _db.Store.GetPartiesAsync(includeArchived: true));
        loaded.IsArchived = false;
        await _db.Store.UpdatePartyAsync(loaded);

        Assert.Single(await _db.Store.GetPartiesAsync());
    }

    /// <summary>
    /// The edit survives a restart. Everything above runs through one context
    /// factory over one file; this is the one that proves it reached the disk.
    /// </summary>
    [Fact]
    public async Task Edits_SurviveAnAppRestart()
    {
        var party = new Party { Name = "Before" };
        await _db.Store.AddPartyAsync(party);
        var loaded = await _db.Store.GetPartyAsync(party.Id);
        loaded!.Name = "After";
        loaded.Phone = "0999";
        await _db.Store.UpdatePartyAsync(loaded);

        var reopened = _db.ReopenStore();
        var reread = await reopened.GetPartyAsync(party.Id);
        Assert.Equal("After", reread!.Name);
        Assert.Equal("0999", reread.Phone);
    }

    /// <summary>
    /// Documents the failure mode a caller has to know about: <c>Update</c> on a
    /// party the database has never seen is an UPDATE matching no rows, and EF
    /// raises that as a concurrency failure rather than inserting. A future edit
    /// screen must not use this method to create parties.
    /// </summary>
    [Fact]
    public async Task Updating_APartyThatWasNeverAdded_Throws_RatherThanInsertingIt()
    {
        var ghost = new Party { Name = "Never added" };

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => _db.Store.UpdatePartyAsync(ghost));
        Assert.Empty(await _db.Store.GetPartiesAsync(includeArchived: true));
    }
}
