using Microsoft.Maui.Graphics;
using Oab.App.Views;
using Oab.Core.Domain;

namespace Oab.App.Tests;

public class PartyStatementViewModelTests
{
    private static PartyStatementViewModel NewVm(VmContext c) =>
        new(c.Store, c.Money, c.Localization);

    private static async Task<Party> AddPartyAsync(VmContext c, string name = "Acme")
    {
        var party = new Party { Name = name, Roles = PartyRole.Supplier };
        await c.Store.AddPartyAsync(party);
        return party;
    }

    [Fact]
    public async Task RunningBalanceAccumulatesInTheOrderMoneyMoved()
    {
        var c = new VmContext();
        var party = await AddPartyAsync(c);
        var day1 = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

        await c.Ledger.RecordPurchaseAsync(party.Id, 100m, paidNow: false, day1);
        await c.Ledger.RecordPurchaseAsync(party.Id, 50m, paidNow: false, day1.AddDays(1));
        await c.Ledger.RecordPaymentOutAsync(party.Id, 30m, day1.AddDays(2));

        var vm = NewVm(c);
        await vm.LoadAsync(party.Id);

        // Newest first on screen, so read the running balances backwards.
        var balances = vm.Rows.Reverse().Select(r => r.BalanceAfterText).ToList();
        Assert.Equal("You owe them: 100.00 SP", balances[0]);
        Assert.Equal("You owe them: 150.00 SP", balances[1]);
        Assert.Equal("You owe them: 120.00 SP", balances[2]);
    }

    [Fact]
    public async Task NewestEntryIsShownFirstAndHeaderMatchesTheLastBalance()
    {
        var c = new VmContext();
        var party = await AddPartyAsync(c);
        var day1 = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

        await c.Ledger.RecordPurchaseAsync(party.Id, 100m, paidNow: false, day1);
        await c.Ledger.RecordPaymentOutAsync(party.Id, 40m, day1.AddDays(1));

        var vm = NewVm(c);
        await vm.LoadAsync(party.Id);

        Assert.Equal("Acme", vm.PartyName);
        Assert.Equal("You paid", vm.Rows[0].KindText);
        Assert.Equal("40.00 SP", vm.Rows[0].AmountText);
        Assert.Equal("Purchase", vm.Rows[1].KindText);
        Assert.Equal(vm.Rows[0].BalanceAfterText, vm.BalanceText);
        Assert.Equal("You owe them: 60.00 SP", vm.BalanceText);
    }

    [Fact]
    public async Task EntriesOutOfChronologicalOrderAreStillSummedInOrder()
    {
        var c = new VmContext();
        var party = await AddPartyAsync(c);
        var day1 = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

        // Backdated: recorded second, happened first.
        await c.Ledger.RecordPaymentOutAsync(party.Id, 40m, day1.AddDays(5));
        await c.Ledger.RecordPurchaseAsync(party.Id, 100m, paidNow: false, day1);

        var vm = NewVm(c);
        await vm.LoadAsync(party.Id);

        Assert.Equal("You paid", vm.Rows[0].KindText);
        Assert.Equal("Purchase", vm.Rows[1].KindText);
        Assert.Equal("You owe them: 100.00 SP", vm.Rows[1].BalanceAfterText);
        Assert.Equal("You owe them: 60.00 SP", vm.BalanceText);
    }

    [Fact]
    public async Task SaleThenPaymentInReadsAsTheyOweYouThenSettled()
    {
        var c = new VmContext();
        var party = await AddPartyAsync(c, "Zeina");
        var day1 = new DateTimeOffset(2026, 2, 1, 9, 0, 0, TimeSpan.Zero);

        await c.Ledger.RecordSaleAsync(party.Id, 75m, paidNow: false, day1);
        await c.Ledger.RecordPaymentInAsync(party.Id, 75m, day1.AddDays(1));

        var vm = NewVm(c);
        await vm.LoadAsync(party.Id);

        Assert.Equal("They paid", vm.Rows[0].KindText);
        Assert.Equal("They owe you: 75.00 SP", vm.Rows[1].BalanceAfterText);
        Assert.Equal("Settled", vm.BalanceText);
    }

    [Fact]
    public async Task CorrectionIsLabelledAndShowsItsReason()
    {
        var c = new VmContext();
        var party = await AddPartyAsync(c);
        var day1 = new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

        await c.Ledger.RecordPurchaseAsync(party.Id, 1000m, paidNow: false, day1);
        // Typed 1000 instead of 100 — the fix is a new entry, not an edit.
        await c.Ledger.RecordAdjustmentAsync(party.Id, 900m, day1.AddMinutes(5), "typo: was 1000");

        var vm = NewVm(c);
        await vm.LoadAsync(party.Id);

        var correction = vm.Rows[0];
        Assert.Equal("Correction", correction.KindText);
        Assert.True(correction.IsCorrection);
        Assert.True(correction.HasNote);
        Assert.Equal("typo: was 1000", correction.NoteText);
        Assert.Equal("You owe them: 100.00 SP", vm.BalanceText);
        // The purchase it corrects is still there, unedited, and not flagged.
        Assert.False(vm.Rows[1].IsCorrection);
        Assert.Equal(1000m, Math.Abs(vm.Rows[1].Entry.Amount));
    }

    [Fact]
    public async Task PartyWithNoEntriesShowsSettledAndNoRows()
    {
        var c = new VmContext();
        var party = await AddPartyAsync(c);

        var vm = NewVm(c);
        await vm.LoadAsync(party.Id);

        Assert.Empty(vm.Rows);
        Assert.Equal("Acme", vm.PartyName);
        Assert.Equal("Settled", vm.BalanceText);
    }

    [Fact]
    public async Task OtherPartiesEntriesDoNotLeakIn()
    {
        var c = new VmContext();
        var mine = await AddPartyAsync(c, "Acme");
        var other = await AddPartyAsync(c, "Other");
        var day1 = new DateTimeOffset(2026, 4, 1, 9, 0, 0, TimeSpan.Zero);

        await c.Ledger.RecordPurchaseAsync(mine.Id, 10m, paidNow: false, day1);
        await c.Ledger.RecordPurchaseAsync(other.Id, 999m, paidNow: false, day1);

        var vm = NewVm(c);
        await vm.LoadAsync(mine.Id);

        Assert.Single(vm.Rows);
        Assert.Equal("You owe them: 10.00 SP", vm.BalanceText);
    }

    [Fact]
    public async Task ReloadingDoesNotDuplicateRows()
    {
        var c = new VmContext();
        var party = await AddPartyAsync(c);
        await c.Ledger.RecordPurchaseAsync(party.Id, 10m, paidNow: false, DateTimeOffset.Now);

        var vm = NewVm(c);
        await vm.LoadAsync(party.Id);
        await vm.LoadAsync(party.Id);

        Assert.Single(vm.Rows);
    }

    [Theory]
    // Opened from the supplier list, red means "you still owe them"...
    [InlineData(PartyRole.Supplier, -100.0, "Firebrick")]
    [InlineData(PartyRole.Supplier, 100.0, "SeaGreen")]
    // ...and from the customer list it means "they still owe you". Same word,
    // opposite sign — which is why the caller has to say which list it is.
    [InlineData(PartyRole.Customer, 100.0, "Firebrick")]
    [InlineData(PartyRole.Customer, -100.0, "SeaGreen")]
    // No expected direction: nothing is alarming, so nothing is coloured.
    [InlineData(PartyRole.None, -100.0, "Gray")]
    [InlineData(PartyRole.None, 100.0, "Gray")]
    [InlineData(PartyRole.Supplier | PartyRole.Customer, -100.0, "Gray")]
    public async Task BalanceColorMatchesTheListItWasOpenedFrom(
        PartyRole perspective, double balance, string expected)
    {
        var c = new VmContext();
        var party = await AddPartyAsync(c);
        var amount = (decimal)Math.Abs(balance);
        // Negative balance = the shop owes them.
        if (balance < 0)
            await c.Ledger.RecordPurchaseAsync(party.Id, amount, paidNow: false, DateTimeOffset.Now);
        else
            await c.Ledger.RecordSaleAsync(party.Id, amount, paidNow: false, DateTimeOffset.Now);

        var vm = NewVm(c);
        await vm.LoadAsync(party.Id, perspective);

        var expectedColor = expected switch
        {
            "Firebrick" => Colors.Firebrick,
            "SeaGreen" => Colors.SeaGreen,
            _ => Colors.Gray,
        };
        Assert.Equal(expectedColor, vm.BalanceColor);
    }

    [Fact]
    public async Task SettledIsNeverAlarming()
    {
        var c = new VmContext();
        var party = await AddPartyAsync(c);
        var day1 = new DateTimeOffset(2026, 5, 1, 9, 0, 0, TimeSpan.Zero);
        await c.Ledger.RecordPurchaseAsync(party.Id, 60m, paidNow: false, day1);
        await c.Ledger.RecordPaymentOutAsync(party.Id, 60m, day1.AddDays(1));

        var vm = NewVm(c);
        await vm.LoadAsync(party.Id, PartyRole.Supplier);

        Assert.Equal("Settled", vm.BalanceText);
        Assert.Equal(Colors.Gray, vm.BalanceColor);
    }

    [Fact]
    public async Task ArabicShowsTranslatedLabels()
    {
        var c = new VmContext(culture: "ar");
        var party = await AddPartyAsync(c);
        await c.Ledger.RecordPurchaseAsync(party.Id, 10m, paidNow: false, DateTimeOffset.Now);

        var vm = NewVm(c);
        await vm.LoadAsync(party.Id);

        Assert.Equal("شراء", vm.Rows[0].KindText);
        Assert.StartsWith("عليك", vm.BalanceText);
    }
}
