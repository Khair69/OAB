using Oab.Core.Domain;
using Oab.Data.Backup;

namespace Oab.Data.Tests;

/// <summary>
/// The data-safety guarantee: a shop must be able to get its book off a dying
/// phone and back onto a new one with nothing lost.
/// </summary>
public sealed class DatabaseBackupTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new("oab-backup");
    private readonly DatabaseBackupService _backup;
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 10, 0, 0, TimeSpan.FromHours(3));

    public DatabaseBackupTests() =>
        _backup = new DatabaseBackupService(_db, new OabDatabase(_db.FilePath));

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Snapshot_ThenRestore_PreservesTheWholeBook()
    {
        var supplier = new Party { Name = "Distributor", Roles = PartyRole.Supplier };
        await _db.Store.AddPartyAsync(supplier);
        await _db.Ledger.RecordPurchaseAsync(supplier.Id, 250m, paidNow: false, Now);
        await _db.Ledger.RecordPaymentOutAsync(supplier.Id, 100m, Now.AddDays(1));

        var backupPath = await _backup.CreateSnapshotAsync(_db.PathTo("book.db"));
        Assert.True(File.Exists(backupPath));
        Assert.True(await _backup.IsValidBackupAsync(backupPath));

        // Disaster: everything after the backup is lost when we go back.
        await _db.Ledger.RecordPurchaseAsync(supplier.Id, 999m, paidNow: false, Now.AddDays(2));
        Assert.Equal(-1149m, await _db.Store.GetPartyBalanceAsync(supplier.Id));

        await _backup.RestoreFromAsync(backupPath);

        Assert.Equal(-150m, await _db.Store.GetPartyBalanceAsync(supplier.Id));
        Assert.Equal("Distributor", (await _db.Store.GetPartyAsync(supplier.Id))?.Name);
        Assert.Single(await _db.Store.GetPartiesAsync());
    }

    [Fact]
    public async Task Restore_KeepsAPreRestoreCopyOfWhatWasThere()
    {
        var party = new Party { Name = "Before" };
        await _db.Store.AddPartyAsync(party);
        var backupPath = await _backup.CreateSnapshotAsync(_db.PathTo("book.db"));

        await _backup.RestoreFromAsync(backupPath);

        // The safety net: the pre-restore book is still on disk and readable.
        var safetyCopy = _db.FilePath + ".pre-restore";
        Assert.True(File.Exists(safetyCopy));
        Assert.True(await _backup.IsValidBackupAsync(safetyCopy));
    }

    [Fact]
    public async Task Snapshot_OverwritesAnExistingFile()
    {
        var target = _db.PathTo("book.db");
        await File.WriteAllTextAsync(target, "stale contents");

        await _backup.CreateSnapshotAsync(target);

        Assert.True(await _backup.IsValidBackupAsync(target));
    }

    [Fact]
    public async Task JunkFile_IsRejected_AndRestoreRefusesIt()
    {
        var junk = _db.PathTo("photo.jpg");
        await File.WriteAllTextAsync(junk, "this is not a database");

        Assert.False(await _backup.IsValidBackupAsync(junk));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _backup.RestoreFromAsync(junk));
    }

    [Fact]
    public async Task MissingFile_IsRejected() =>
        Assert.False(await _backup.IsValidBackupAsync(_db.PathTo("nope.db")));

    [Fact]
    public async Task ForeignSqliteDatabase_IsRejected()
    {
        // A real SQLite file, but not our schema — e.g. some other app's data.
        var foreign = _db.PathTo("other-app.db");
        await using (var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={foreign}"))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "CREATE TABLE Recipes (Id INTEGER PRIMARY KEY)";
            await cmd.ExecuteNonQueryAsync();
        }
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        Assert.False(await _backup.IsValidBackupAsync(foreign));
    }
}
