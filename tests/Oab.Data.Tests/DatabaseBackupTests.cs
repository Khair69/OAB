using Microsoft.EntityFrameworkCore;
using Oab.Core.Domain;
using Oab.Core.Ledger;
using Oab.Data;
using Oab.Data.Backup;

namespace Oab.Data.Tests;

/// <summary>
/// The data-safety guarantee: a shop must be able to get its book off a dying
/// phone and back onto a new one with nothing lost.
/// </summary>
public sealed class DatabaseBackupTests : IDisposable
{
    private sealed class TestDbFactory(DbContextOptions<OabDbContext> options) : IDbContextFactory<OabDbContext>
    {
        public OabDbContext CreateDbContext() => new(options);
    }

    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"oab-backup-{Guid.NewGuid():N}");
    private readonly string _dbPath;
    private readonly LedgerStore _store;
    private readonly LedgerService _ledger;
    private readonly DatabaseBackupService _backup;
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 10, 0, 0, TimeSpan.FromHours(3));

    public DatabaseBackupTests()
    {
        Directory.CreateDirectory(_dir);
        _dbPath = Path.Combine(_dir, "oab.db");
        var options = new DbContextOptionsBuilder<OabDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        using (var db = new OabDbContext(options))
            db.Database.Migrate();

        var factory = new TestDbFactory(options);
        _store = new LedgerStore(factory);
        _ledger = new LedgerService(_store);
        _backup = new DatabaseBackupService(factory, new OabDatabase(_dbPath));
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // A stray handle on Windows shouldn't fail the test run.
        }
    }

    [Fact]
    public async Task Snapshot_ThenRestore_PreservesTheWholeBook()
    {
        var supplier = new Party { Name = "Distributor", Roles = PartyRole.Supplier };
        await _store.AddPartyAsync(supplier);
        await _ledger.RecordPurchaseAsync(supplier.Id, 250m, paidNow: false, Now);
        await _ledger.RecordPaymentOutAsync(supplier.Id, 100m, Now.AddDays(1));

        var backupPath = await _backup.CreateSnapshotAsync(Path.Combine(_dir, "book.db"));
        Assert.True(File.Exists(backupPath));
        Assert.True(await _backup.IsValidBackupAsync(backupPath));

        // Disaster: everything after the backup is lost when we go back.
        await _ledger.RecordPurchaseAsync(supplier.Id, 999m, paidNow: false, Now.AddDays(2));
        Assert.Equal(-1149m, await _store.GetPartyBalanceAsync(supplier.Id));

        await _backup.RestoreFromAsync(backupPath);

        Assert.Equal(-150m, await _store.GetPartyBalanceAsync(supplier.Id));
        Assert.Equal("Distributor", (await _store.GetPartyAsync(supplier.Id))?.Name);
        Assert.Single(await _store.GetPartiesAsync());
    }

    [Fact]
    public async Task Restore_KeepsAPreRestoreCopyOfWhatWasThere()
    {
        var party = new Party { Name = "Before" };
        await _store.AddPartyAsync(party);
        var backupPath = await _backup.CreateSnapshotAsync(Path.Combine(_dir, "book.db"));

        await _backup.RestoreFromAsync(backupPath);

        // The safety net: the pre-restore book is still on disk and readable.
        var safetyCopy = _dbPath + ".pre-restore";
        Assert.True(File.Exists(safetyCopy));
        Assert.True(await _backup.IsValidBackupAsync(safetyCopy));
    }

    [Fact]
    public async Task Snapshot_OverwritesAnExistingFile()
    {
        var target = Path.Combine(_dir, "book.db");
        await File.WriteAllTextAsync(target, "stale contents");

        await _backup.CreateSnapshotAsync(target);

        Assert.True(await _backup.IsValidBackupAsync(target));
    }

    [Fact]
    public async Task JunkFile_IsRejected_AndRestoreRefusesIt()
    {
        var junk = Path.Combine(_dir, "photo.jpg");
        await File.WriteAllTextAsync(junk, "this is not a database");

        Assert.False(await _backup.IsValidBackupAsync(junk));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _backup.RestoreFromAsync(junk));
    }

    [Fact]
    public async Task MissingFile_IsRejected() =>
        Assert.False(await _backup.IsValidBackupAsync(Path.Combine(_dir, "nope.db")));

    [Fact]
    public async Task ForeignSqliteDatabase_IsRejected()
    {
        // A real SQLite file, but not our schema — e.g. some other app's data.
        var foreign = Path.Combine(_dir, "other-app.db");
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
