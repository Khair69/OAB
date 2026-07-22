using Oab.Core.Domain;
using Oab.Data.Backup;
using Oab.Modules.Backup;

namespace Oab.App.Tests;

public class BackupViewModelTests
{
    /// <summary>The summary is built from the ledger, so the file layer can be inert here.</summary>
    private sealed class NoopBackup : IDatabaseBackup
    {
        public Task<string> CreateSnapshotAsync(string destinationPath, CancellationToken ct = default) =>
            Task.FromResult(destinationPath);

        public Task<bool> IsValidBackupAsync(string candidatePath, CancellationToken ct = default) =>
            Task.FromResult(true);

        public Task RestoreFromAsync(string backupPath, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    [Fact]
    public async Task Summary_ShowsBothSidesOfTheBook_Localized()
    {
        var c = new VmContext();
        var supplier = new Party { Name = "Acme", Roles = PartyRole.Supplier };
        var customer = new Party { Name = "Sami", Roles = PartyRole.Customer };
        await c.Store.AddPartyAsync(supplier);
        await c.Store.AddPartyAsync(customer);
        await c.Ledger.RecordPurchaseAsync(supplier.Id, 500m, paidNow: false, DateTimeOffset.Now);
        await c.Ledger.RecordSaleAsync(customer.Id, 80m, paidNow: false, DateTimeOffset.Now);

        var vm = new BackupViewModel(new NoopBackup(), c.Store, c.Config, c.Localization, c.ErrorLog);
        var text = await vm.BuildSummaryTextAsync();

        Assert.Contains("Test Shop", text, StringComparison.Ordinal);
        Assert.Contains("- Acme: 500.00 SP", text, StringComparison.Ordinal);
        Assert.Contains("- Sami: 80.00 SP", text, StringComparison.Ordinal);
        // Headings come from Strings.resx through LocalizationManager.
        Assert.Contains("You owe", text, StringComparison.Ordinal);
        Assert.Contains("Owed to you", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Summary_OnAnEmptyBook_IsStillReadable()
    {
        var c = new VmContext();
        var vm = new BackupViewModel(new NoopBackup(), c.Store, c.Config, c.Localization, c.ErrorLog);

        var text = await vm.BuildSummaryTextAsync();

        Assert.Contains("Test Shop", text, StringComparison.Ordinal);
        Assert.Contains("Settled accounts: 0", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The card offering to send the error report is bound to this. On a phone
    /// that has never crashed it must stay hidden, or every shop sees a button
    /// they cannot explain.
    /// </summary>
    [Fact]
    public void ErrorLogCard_IsHiddenUntilSomethingHasGoneWrong()
    {
        var c = new VmContext();
        var vm = new BackupViewModel(new NoopBackup(), c.Store, c.Config, c.Localization, c.ErrorLog);

        Assert.False(vm.HasErrorLog);

        c.ErrorLog.Write("test", new InvalidOperationException("boom"));
        vm.Refresh();

        Assert.True(vm.HasErrorLog);
    }
}
