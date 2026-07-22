using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using Oab.App;
using Oab.App.Diagnostics;
using Oab.App.Localization;
using Oab.Core.Ledger;
using Oab.Core.Reporting;
using Oab.Data.Backup;

namespace Oab.Modules.Backup;

/// <summary>
/// Two kinds of copy, because they fail differently: the <c>.db</c> snapshot
/// restores perfectly but only into this app, while the text summary can be
/// read by a human on any phone even if the app is gone entirely.
/// </summary>
public partial class BackupViewModel(
    IDatabaseBackup backup,
    ILedgerStore store,
    ShopConfig config,
    LocalizationManager localization,
    ErrorLog errorLog) : ObservableObject
{
    [ObservableProperty]
    public partial string Status { get; set; } = "";

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    /// <summary>
    /// Whether anything has crashed since the log was last cleared. The page
    /// hides the "send the error log" card unless this is true: on a healthy
    /// phone the offer is noise, and a button whose purpose the shopkeeper cannot
    /// guess is a button that makes the app feel broken.
    /// </summary>
    public bool HasErrorLog => errorLog.HasEntries;

    /// <summary>
    /// Re-reads <see cref="HasErrorLog"/>. It is a file on disk, not observable
    /// state, so nothing tells the UI when it changes — the page asks on
    /// appearing and after every action.
    /// </summary>
    public void Refresh() => OnPropertyChanged(nameof(HasErrorLog));

    /// <summary>Writes a restorable snapshot to the cache and returns its path.</summary>
    public async Task<string> CreateDatabaseSnapshotAsync(CancellationToken ct = default)
    {
        var path = Path.Combine(FileSystem.CacheDirectory, $"{FileStem()}.db");
        return await backup.CreateSnapshotAsync(path, ct);
    }

    /// <summary>Writes the human-readable summary to the cache and returns its path.</summary>
    public async Task<string> CreateSummaryFileAsync(CancellationToken ct = default)
    {
        var path = Path.Combine(FileSystem.CacheDirectory, $"{FileStem()}.txt");
        await File.WriteAllTextAsync(path, await BuildSummaryTextAsync(ct), new UTF8Encoding(true), ct);
        return path;
    }

    public async Task<string> BuildSummaryTextAsync(CancellationToken ct = default)
    {
        var parties = await store.GetPartiesAsync(includeArchived: true, ct: ct);
        var balances = await store.GetBalancesAsync(ct);

        var lines = parties.Select(p => new SummaryLine(p.Name, balances.GetValueOrDefault(p.Id)));
        var labels = new SummaryLabels(
            localization["Backup_YouOwe"],
            localization["Backup_OwesYou"],
            localization["Backup_Settled"],
            localization["Backup_Total"]);

        return LedgerSummaryReport.Build(
            config.ShopName, DateTimeOffset.Now, lines, labels,
            localization.Culture, config.CurrencySymbol, config.UseArabicIndicDigits);
    }

    /// <summary>
    /// Copies the log into the cache under a shop-and-date filename and returns
    /// its path. A copy rather than the original for two reasons: the share
    /// target keeps a handle on the file while the app may still be appending to
    /// it, and <c>errors.log</c> arriving in someone's WhatsApp says nothing
    /// about which shop or which day it came from. Same directory and the same
    /// naming as the backups, for the same reasons.
    /// </summary>
    public async Task<string> CreateErrorLogFileAsync(CancellationToken ct = default)
    {
        var path = Path.Combine(FileSystem.CacheDirectory, $"{FileStem()}-errors.txt");
        await File.WriteAllTextAsync(path, errorLog.ReadAll(), new UTF8Encoding(true), ct);
        return path;
    }

    public Task<bool> IsValidBackupAsync(string path, CancellationToken ct = default) =>
        backup.IsValidBackupAsync(path, ct);

    public Task RestoreAsync(string path, CancellationToken ct = default) =>
        backup.RestoreFromAsync(path, ct);

    /// <summary>Shop name and timestamp, safe for a filename on any platform.</summary>
    private string FileStem()
    {
        var name = new string(config.ShopName
            .Select(c => Path.GetInvalidFileNameChars().Contains(c) || c == ' ' ? '-' : c)
            .ToArray());
        return $"{name}-{DateTime.Now:yyyy-MM-dd-HHmm}";
    }
}
