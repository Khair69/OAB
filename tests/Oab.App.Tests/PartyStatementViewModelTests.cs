using Microsoft.Maui.Graphics;
using Oab.App.Views;
using Oab.Core.Domain;

namespace Oab.App.Tests;

public class PartyStatementViewModelTests
{
    private static PartyStatementViewModel NewVm(VmContext c) =>
        new(c.Store, c.Ledger, c.Money, c.Localization);

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
    public async Task CorrectingAnEntryFixesTheBalanceWithoutTouchingHistory()
    {
        var c = new VmContext();
        var party = await AddPartyAsync(c);
        var day1 = new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);
        // The fat-finger this whole feature exists for.
        await c.Ledger.RecordPurchaseAsync(party.Id, 1000m, paidNow: false, day1);

        var vm = NewVm(c);
        await vm.LoadAsync(party.Id, PartyRole.Supplier);
        var outcome = await vm.CorrectAsync(vm.Rows[0], 100m, "typo");

        Assert.Equal(CorrectionOutcome.Applied, outcome);
        Assert.Equal("You owe them: 100.00 SP", vm.BalanceText);
        // Two rows now: the correction on top, the original still saying 1000.
        Assert.Equal(2, vm.Rows.Count);
        Assert.True(vm.Rows[0].IsCorrection);
        Assert.Equal("typo", vm.Rows[0].NoteText);
        Assert.Equal(-1000m, vm.Rows[1].Entry.Amount);
        Assert.False(vm.Rows[1].IsCorrection);
    }

    [Fact]
    public async Task CorrectingToZeroCancelsTheEntryButLeavesItOnTheRecord()
    {
        var c = new VmContext();
        var party = await AddPartyAsync(c);
        await c.Ledger.RecordSaleAsync(party.Id, 250m, paidNow: false, DateTimeOffset.Now);

        var vm = NewVm(c);
        await vm.LoadAsync(party.Id, PartyRole.Customer);
        var outcome = await vm.CorrectAsync(vm.Rows[0], 0m, "never happened");

        Assert.Equal(CorrectionOutcome.Applied, outcome);
        Assert.Equal("Settled", vm.BalanceText);
        Assert.Equal(2, vm.Rows.Count);
    }

    [Fact]
    public async Task CorrectionInheritsTheDocumentSoTheInvoiceStopsAskingForTheOldAmount()
    {
        var c = new VmContext();
        var party = await AddPartyAsync(c);
        var document = await c.Ledger.RecordPurchaseAsync(
            party.Id, 1000m, paidNow: false, DateTimeOffset.Now);

        var vm = NewVm(c);
        await vm.LoadAsync(party.Id, PartyRole.Supplier);
        await vm.CorrectAsync(vm.Rows[0], 100m, "typo");

        // Without the inherited DocumentId the purchases list would still be
        // offering to pay off 1000.
        Assert.Equal(100m, await c.Ledger.GetDocumentOutstandingAsync(document.Id));
        Assert.Equal(document.Id, vm.Rows[0].Entry.DocumentId);
    }

    [Fact]
    public async Task CorrectingAPaymentAdjustsInThePaymentsOwnDirection()
    {
        var c = new VmContext();
        var party = await AddPartyAsync(c);
        var day1 = new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);
        await c.Ledger.RecordPurchaseAsync(party.Id, 500m, paidNow: false, day1);
        await c.Ledger.RecordPaymentOutAsync(party.Id, 300m, day1.AddDays(1));

        var vm = NewVm(c);
        await vm.LoadAsync(party.Id, PartyRole.Supplier);
        // Paid 200, not 300 — the debt should go back up, not down.
        var outcome = await vm.CorrectAsync(vm.Rows[0], 200m, "paid 200");

        Assert.Equal(CorrectionOutcome.Applied, outcome);
        Assert.Equal("You owe them: 300.00 SP", vm.BalanceText);
    }

    [Fact]
    public async Task ACorrectionCanItselfBeCorrected()
    {
        var c = new VmContext();
        var party = await AddPartyAsync(c);
        await c.Ledger.RecordPurchaseAsync(party.Id, 1000m, paidNow: false, DateTimeOffset.Now);

        var vm = NewVm(c);
        await vm.LoadAsync(party.Id, PartyRole.Supplier);
        await vm.CorrectAsync(vm.Rows[0], 100m, "typo");        // delta +900
        await vm.CorrectAsync(vm.Rows[0], 800m, "wrong again");  // that 900 should have been 800

        Assert.Equal("You owe them: 200.00 SP", vm.BalanceText);
        Assert.Equal(3, vm.Rows.Count);
    }

    [Theory]
    [InlineData(-1.0, "typo", CorrectionOutcome.InvalidAmount)]
    [InlineData(100.0, "", CorrectionOutcome.NoteMissing)]
    [InlineData(100.0, "   ", CorrectionOutcome.NoteMissing)]
    [InlineData(null, null, CorrectionOutcome.NoteMissing)]
    public async Task ARefusedCorrectionPostsNothing(
        double? correctedAmount, string? note, CorrectionOutcome expected)
    {
        var c = new VmContext();
        var party = await AddPartyAsync(c);
        await c.Ledger.RecordPurchaseAsync(party.Id, 1000m, paidNow: false, DateTimeOffset.Now);

        var vm = NewVm(c);
        await vm.LoadAsync(party.Id, PartyRole.Supplier);
        var outcome = await vm.CorrectAsync(vm.Rows[0], (decimal)(correctedAmount ?? 100.0), note);

        Assert.Equal(expected, outcome);
        Assert.Single(vm.Rows);
        Assert.Equal("You owe them: 1,000.00 SP", vm.BalanceText);
    }

    [Fact]
    public async Task CorrectingToTheAmountAlreadyRecordedPostsNothing()
    {
        var c = new VmContext();
        var party = await AddPartyAsync(c);
        await c.Ledger.RecordPurchaseAsync(party.Id, 1000m, paidNow: false, DateTimeOffset.Now);

        var vm = NewVm(c);
        await vm.LoadAsync(party.Id, PartyRole.Supplier);
        // An adjustment of zero is rejected by the engine; catching it here is
        // what turns a crash into a sentence.
        var outcome = await vm.CorrectAsync(vm.Rows[0], 1000m, "no change");

        Assert.Equal(CorrectionOutcome.AlreadyThatAmount, outcome);
        Assert.Single(vm.Rows);
    }

    [Fact]
    public async Task ThePromptsShowWhatIsRecordedAndSuggestItAsTheReason()
    {
        var c = new VmContext();
        var party = await AddPartyAsync(c);
        await c.Ledger.RecordPurchaseAsync(party.Id, 1000m, paidNow: false, DateTimeOffset.Now);

        var vm = NewVm(c);
        await vm.LoadAsync(party.Id);
        var row = vm.Rows[0];

        Assert.Equal(
            "Purchase — recorded as: 1,000.00 SP\nWhat should the amount have been?",
            vm.CorrectionPromptFor(row));
        Assert.Equal("Was 1,000.00 SP", vm.SuggestedNoteFor(row));
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
