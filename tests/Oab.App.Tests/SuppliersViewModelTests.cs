using Microsoft.Maui.Graphics;
using Oab.Core.Domain;
using Oab.Modules.SupplierDebts;

namespace Oab.App.Tests;

public class SuppliersViewModelTests
{
    private static SuppliersViewModel NewVm(VmContext c) =>
        new(c.Store, c.Ledger, c.Money, c.Localization);

    [Fact]
    public async Task PurchaseOnCredit_ShowsYouOweThem()
    {
        var c = new VmContext();
        var vm = NewVm(c);
        await vm.AddSupplierAsync("Acme");
        var row = Assert.Single(vm.Suppliers);

        await c.Ledger.RecordPurchaseAsync(row.Party.Id, 100m, paidNow: false, DateTimeOffset.Now);
        await vm.LoadAsync();

        row = Assert.Single(vm.Suppliers);
        Assert.True(row.HasDebt);
        Assert.Equal(Colors.Firebrick, row.BalanceColor);
        Assert.Contains("100.00 SP", row.BalanceText);
    }

    [Fact]
    public async Task RecordPayment_SettlesTheSupplier()
    {
        var c = new VmContext();
        var vm = NewVm(c);
        await vm.AddSupplierAsync("Acme");
        await c.Ledger.RecordPurchaseAsync(Assert.Single(vm.Suppliers).Party.Id, 100m, paidNow: false, DateTimeOffset.Now);
        await vm.LoadAsync();

        await vm.RecordPaymentAsync(Assert.Single(vm.Suppliers), 100m);

        var row = Assert.Single(vm.Suppliers);
        Assert.False(row.HasDebt);
        Assert.Equal(Colors.Gray, row.BalanceColor);
    }

    [Fact]
    public async Task CustomersAreNotShownInSupplierList()
    {
        var c = new VmContext();
        await c.Store.AddPartyAsync(new Party { Name = "Walk-in buyer", Roles = PartyRole.Customer });
        var vm = NewVm(c);

        await vm.LoadAsync();

        Assert.Empty(vm.Suppliers);
    }
}
