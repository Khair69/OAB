namespace Oab.Data.Backup;

/// <summary>Absolute path to this shop's SQLite file.</summary>
public sealed record OabDatabase(string Path);

/// <summary>
/// Getting the shop's book off the device and back on again. This is the
/// single most important safety feature in the product: without it, a lost or
/// broken phone loses the entire ledger — worse than the paper notebook it replaces.
/// </summary>
public interface IDatabaseBackup
{
    /// <summary>
    /// Writes a consistent snapshot of the live database to
    /// <paramref name="destinationPath"/> and returns that path.
    /// </summary>
    Task<string> CreateSnapshotAsync(string destinationPath, CancellationToken ct = default);

    /// <summary>
    /// True if the file really is an OAB book. Guards restore against a user
    /// picking a photo, a half-downloaded file, or some other app's database.
    /// </summary>
    Task<bool> IsValidBackupAsync(string candidatePath, CancellationToken ct = default);

    /// <summary>
    /// Replaces the live database with <paramref name="backupPath"/>, keeping a
    /// <c>.pre-restore</c> copy of what was there. Throws if the file isn't a
    /// valid backup. The app should be restarted afterwards.
    /// </summary>
    Task RestoreFromAsync(string backupPath, CancellationToken ct = default);
}
