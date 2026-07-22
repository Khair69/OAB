using Microsoft.Maui.Graphics;
using Oab.Core.Domain;
using Oab.Modules.CustomerDebts;

namespace Oab.App.Tests;

public class CustomersViewModelTests
{
    private static CustomersViewModel NewVm(VmContext c) =>
        new(c.Store, c.Ledger, c.Money, c.Localization);

    [Fact]
    public async Task AddCustomer_StartsSettled_ThenDebtShowsOwesYou()
    {
        var c = new VmContext();
        var vm = NewVm(c);

        await vm.AddCustomerAsync("Sami");
        var row = Assert.Single(vm.Customers);
        Assert.False(row.OwesYou);
        Assert.Equal(Colors.Gray, row.BalanceColor);

        await vm.RecordDebtAsync(row, 80m);
        row = Assert.Single(vm.Customers);
        Assert.True(row.OwesYou);
        Assert.Equal(Colors.Firebrick, row.BalanceColor);
        Assert.Contains("80.00 SP", row.BalanceText);
    }

    [Fact]
    public async Task CollectPayment_ClearsTheDebt()
    {
        var c = new VmContext();
        var vm = NewVm(c);
        await vm.AddCustomerAsync("Sami");
        await vm.RecordDebtAsync(Assert.Single(vm.Customers), 80m);

        await vm.RecordPaymentAsync(Assert.Single(vm.Customers), 80m);

        var row = Assert.Single(vm.Customers);
        Assert.False(row.OwesYou);
        Assert.Equal(Colors.Gray, row.BalanceColor);
    }

    [Fact]
    public async Task PartialPayment_StillOwesYou()
    {
        var c = new VmContext();
        var vm = NewVm(c);
        await vm.AddCustomerAsync("Sami");
        await vm.RecordDebtAsync(Assert.Single(vm.Customers), 80m);

        await vm.RecordPaymentAsync(Assert.Single(vm.Customers), 30m);

        var row = Assert.Single(vm.Customers);
        Assert.True(row.OwesYou);
        Assert.Contains("50.00 SP", row.BalanceText);
    }

    [Fact]
    public async Task SuppliersAreNotShownInCustomerList()
    {
        var c = new VmContext();
        await c.Store.AddPartyAsync(new Party { Name = "Distributor", Roles = PartyRole.Supplier });
        var vm = NewVm(c);

        await vm.LoadAsync();

        Assert.Empty(vm.Customers);
    }
}
