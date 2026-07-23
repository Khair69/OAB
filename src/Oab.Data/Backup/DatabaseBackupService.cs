using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Oab.Data.Backup;

public class DatabaseBackupService(
    IDbContextFactory<OabDbContext> contextFactory,
    OabDatabase database) : IDatabaseBackup
{
    /// <summary>If these are all present, the file is an OAB book.</summary>
    private static readonly string[] RequiredTables =
        ["Parties", "Documents", "DocumentLines", "LedgerEntries"];

    public async Task<string> CreateSnapshotAsync(string destinationPath, CancellationToken ct = default)
    {
        // VACUUM INTO refuses to write over an existing file.
        if (File.Exists(destinationPath))
            File.Delete(destinationPath);

        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        await using var db = await contextFactory.CreateDbContextAsync(ct);
        // VACUUM INTO writes a consistent, defragmented copy even while the app
        // still holds the database open — safer than File.Copy, which can catch
        // a half-written page or miss the journal.
        var quoted = destinationPath.Replace("'", "''", StringComparison.Ordinal);
        // Raw SQL, deliberately. VACUUM INTO's target cannot be a parameter, so
        // there is no ExecuteSqlAsync form of it. The one interpolated value is a
        // path this app chose (the share-sheet cache or the app data directory),
        // single-quote escaped above — never user text. Suppressed at the line
        // rather than left to print on every build: a build that always says
        // something teaches everyone to skim past the time it says something new.
#pragma warning disable EF1002
        await db.Database.ExecuteSqlRawAsync($"VACUUM INTO '{quoted}'", ct);
#pragma warning restore EF1002
        return destinationPath;
    }

    public async Task<bool> IsValidBackupAsync(string candidatePath, CancellationToken ct = default)
    {
        if (!File.Exists(candidatePath))
            return false;

        try
        {
            await using var connection = new SqliteConnection(
                new SqliteConnectionStringBuilder
                {
                    DataSource = candidatePath,
                    Mode = SqliteOpenMode.ReadOnly,
                }.ToString());
            await connection.OpenAsync(ct);

            foreach (var table in RequiredTables)
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name";
                command.Parameters.AddWithValue("$name", table);
                if (Convert.ToInt64(await command.ExecuteScalarAsync(ct)) == 0)
                    return false;
            }

            return true;
        }
        catch (SqliteException)
        {
            // Not a SQLite file at all, or it's corrupt — either way, not a backup.
            return false;
        }
    }

    public async Task RestoreFromAsync(string backupPath, CancellationToken ct = default)
    {
        if (!await IsValidBackupAsync(backupPath, ct))
            throw new InvalidOperationException($"'{backupPath}' is not a valid OAB backup.");

        // Never destroy the current book without keeping a copy first.
        if (File.Exists(database.Path))
            File.Copy(database.Path, database.Path + ".pre-restore", overwrite: true);

        // Release pooled handles, then clear any write-ahead files: they belong
        // to the old database and would corrupt the restored one.
        SqliteConnection.ClearAllPools();
        foreach (var sidecar in new[] { database.Path + "-wal", database.Path + "-shm" })
        {
            if (File.Exists(sidecar))
                File.Delete(sidecar);
        }

        File.Copy(backupPath, database.Path, overwrite: true);

        // The backup may predate the current schema; bring it up to date.
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        await db.Database.MigrateAsync(ct);
    }
}
