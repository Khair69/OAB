using Oab.Core.Domain;
using Oab.Modules.Purchases;

namespace Oab.App.Tests;

public class PurchasesViewModelTests
{
    [Fact]
    public async Task CreditPurchase_ListsAsUnpaid_ThenPayRemainingSettlesIt()
    {
        var c = new VmContext();
        var supplier = new Party { Name = "Acme", Roles = PartyRole.Supplier };
        await c.Store.AddPartyAsync(supplier);
        await c.Ledger.RecordPurchaseAsync(supplier.Id, 250m, paidNow: false, DateTimeOffset.Now);

        var vm = new PurchasesListViewModel(c.Store, c.Ledger, c.Money, c.Localization);
        await vm.LoadAsync();

        var row = Assert.Single(vm.Purchases);
        Assert.Equal("Acme", row.SupplierName);
        Assert.True(row.IsUnpaid);
        Assert.Equal(250m, row.Outstanding);
        Assert.Contains("250.00 SP", row.TotalText);

        await vm.PayRemainingCommand.ExecuteAsync(row);

        row = Assert.Single(vm.Purchases);
        Assert.False(row.IsUnpaid);
        Assert.Equal(0m, row.Outstanding);
    }

    [Fact]
    public async Task CashPurchase_ListsAsPaid()
    {
        var c = new VmContext();
        var supplier = new Party { Name = "Acme", Roles = PartyRole.Supplier };
        await c.Store.AddPartyAsync(supplier);
        await c.Ledger.RecordPurchaseAsync(supplier.Id, 60m, paidNow: true, DateTimeOffset.Now);

        var vm = new PurchasesListViewModel(c.Store, c.Ledger, c.Money, c.Localization);
        await vm.LoadAsync();

        Assert.False(Assert.Single(vm.Purchases).IsUnpaid);
    }

    [Fact]
    public async Task NewPurchase_InvalidAmount_SetsError_AndCreatesNoParty()
    {
        var c = new VmContext();
        var vm = new NewPurchaseViewModel(c.Store, c.Ledger, c.Localization)
        {
            AmountText = "not-a-number",
        };

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.False(string.IsNullOrEmpty(vm.Error));
        Assert.Empty(await c.Store.GetPartiesAsync(includeArchived: true));
    }

    [Fact]
    public async Task NewPurchase_NoSupplierChosen_SetsError()
    {
        var c = new VmContext();
        var vm = new NewPurchaseViewModel(c.Store, c.Ledger, c.Localization)
        {
            AmountText = "100",
        };

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.False(string.IsNullOrEmpty(vm.Error));
    }
}
