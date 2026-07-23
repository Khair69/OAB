using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Oab.Core.Ledger;

namespace Oab.Data.Tests;

/// <summary>
/// A throwaway SQLite file in a temp directory with the <em>real</em> migrations
/// applied, plus a store and service wired over it.
///
/// <para>
/// This exists because "it works" and "it works against SQLite" are different
/// claims, and the project has already been bitten twice by the gap: two screens
/// shipped throwing <c>NotSupportedException</c> on every open because no test
/// had run their queries against anything but <c>InMemoryLedgerStore</c>. The
/// standing rule in <c>docs/10-status.md</c> — <em>a store method with no
/// real-SQLite test has never actually run</em> — needs the harness for writing
/// one to cost nothing.
/// </para>
/// <para>
/// It is also one copy of scaffolding that used to be three. Duplicated setup
/// rots the same way duplicated logic does: the copy in the file you are not
/// looking at is the one that is wrong.
/// </para>
/// </summary>
public sealed class SqliteTestDatabase : IDbContextFactory<OabDbContext>, IDisposable
{
    private readonly string _directory;

    /// <summary>The database file, for tests that need it by path (backup/restore).</summary>
    public string FilePath { get; }

    public DbContextOptions<OabDbContext> Options { get; }

    public LedgerStore Store { get; }

    public LedgerService Ledger { get; }

    /// <param name="label">Shows up in the temp directory name when a test leaks one.</param>
    public SqliteTestDatabase(string label = "oab")
    {
        _directory = Path.Combine(Path.GetTempPath(), $"{label}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        FilePath = Path.Combine(_directory, "oab.db");

        Options = BuildOptions(FilePath);
        using (var db = new OabDbContext(Options))
            db.Database.Migrate();

        Store = new LedgerStore(this);
        Ledger = new LedgerService(Store);
    }

    public OabDbContext CreateDbContext() => new(Options);

    /// <summary>
    /// A second store reading the same file through freshly built options — as
    /// close as a test gets to closing the app and opening it again.
    /// </summary>
    public LedgerStore ReopenStore() => new(new PlainFactory(BuildOptions(FilePath)));

    /// <summary>A sibling path inside the same temp directory, for backup targets.</summary>
    public string PathTo(string fileName) => Path.Combine(_directory, fileName);

    public void Dispose()
    {
        // SQLite keeps pooled handles on the file; Windows will not delete it
        // underneath them and the next test run inherits a dirty temp directory.
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // A stray handle shouldn't turn a passing test run red.
        }
    }

    private static DbContextOptions<OabDbContext> BuildOptions(string path) =>
        new DbContextOptionsBuilder<OabDbContext>()
            .UseSqlite($"Data Source={path}")
            .Options;

    private sealed class PlainFactory(DbContextOptions<OabDbContext> options) : IDbContextFactory<OabDbContext>
    {
        public OabDbContext CreateDbContext() => new(options);
    }
}
