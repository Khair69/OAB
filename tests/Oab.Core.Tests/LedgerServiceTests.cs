using Oab.Core.Domain;
using Oab.Core.Ledger;
using Oab.TestSupport;

namespace Oab.Core.Tests;

public class LedgerServiceTests
{
    private readonly InMemoryLedgerStore _store = new();
    private readonly LedgerService _service;
    private readonly Guid _supplier = Guid.NewGuid();
    private readonly Guid _customer = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 7, 8, 10, 0, 0, TimeSpan.FromHours(3));

    public LedgerServiceTests() => _service = new LedgerService(_store);

    [Fact]
    public async Task PurchaseOnCredit_ShopOwesSupplier()
    {
        await _service.RecordPurchaseAsync(_supplier, 100m, paidNow: false, Now);

        Assert.Equal(-100m, await _service.GetPartyBalanceAsync(_supplier));
    }

    [Fact]
    public async Task PurchasePaidNow_NoDebt_DocumentSettled()
    {
        var doc = await _service.RecordPurchaseAsync(_supplier, 100m, paidNow: true, Now);

        Assert.Equal(0m, await _service.GetPartyBalanceAsync(_supplier));
        Assert.Equal(0m, await _service.GetDocumentOutstandingAsync(doc.Id));
        // Cash purchase is still two honest ledger rows: the debt and its settlement.
        Assert.Equal(2, _store.Entries.Count);
    }

    [Fact]
    public async Task PartialPayment_ReducesDebt_DocumentStillOutstanding()
    {
        var doc = await _service.RecordPurchaseAsync(_supplier, 250m, paidNow: false, Now);
        await _service.RecordPaymentOutAsync(_supplier, 100m, Now, doc.Id);

        Assert.Equal(-150m, await _service.GetPartyBalanceAsync(_supplier));
        Assert.Equal(150m, await _service.GetDocumentOutstandingAsync(doc.Id));
    }

    [Fact]
    public async Task FullPaymentInInstallments_SettlesDocument()
    {
        var doc = await _service.RecordPurchaseAsync(_supplier, 250m, paidNow: false, Now);
        await _service.RecordPaymentOutAsync(_supplier, 100m, Now, doc.Id);
        await _service.RecordPaymentOutAsync(_supplier, 150m, Now.AddDays(7), doc.Id);

        Assert.Equal(0m, await _service.GetPartyBalanceAsync(_supplier));
        Assert.Equal(0m, await _service.GetDocumentOutstandingAsync(doc.Id));
    }

    [Fact]
    public async Task SaleOnCredit_CustomerOwesShop()
    {
        await _service.RecordSaleAsync(_customer, 80m, paidNow: false, Now);

        Assert.Equal(80m, await _service.GetPartyBalanceAsync(_customer));
    }

    [Fact]
    public async Task CustomerPaysBack_DebtCleared()
    {
        await _service.RecordSaleAsync(_customer, 80m, paidNow: false, Now);
        await _service.RecordPaymentInAsync(_customer, 80m, Now.AddDays(3));

        Assert.Equal(0m, await _service.GetPartyBalanceAsync(_customer));
    }

    [Fact]
    public async Task SamePartyAsSupplierAndCustomer_OneNetBalance()
    {
        var party = Guid.NewGuid();
        await _service.RecordPurchaseAsync(party, 100m, paidNow: false, Now); // we owe 100
        await _service.RecordSaleAsync(party, 30m, paidNow: false, Now);      // they owe 30

        Assert.Equal(-70m, await _service.GetPartyBalanceAsync(party));
    }

    [Fact]
    public async Task Adjustment_CorrectsBalance_WithoutTouchingHistory()
    {
        await _service.RecordPurchaseAsync(_supplier, 100m, paidNow: false, Now);
        // Shopkeeper realizes the invoice was really 90: correct by +10 (we owe less).
        await _service.RecordAdjustmentAsync(_supplier, 10m, Now, "invoice was 90, not 100");

        Assert.Equal(-90m, await _service.GetPartyBalanceAsync(_supplier));
        Assert.Equal(2, _store.Entries.Count); // original entry untouched
    }

    [Fact]
    public async Task Adjustment_RequiresNoteAndNonZeroAmount()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _service.RecordAdjustmentAsync(_supplier, 0m, Now, "why"));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.RecordAdjustmentAsync(_supplier, 10m, Now, "  "));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task NonPositiveAmounts_AreRejected(decimal amount)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _service.RecordPurchaseAsync(_supplier, amount, paidNow: false, Now));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _service.RecordPaymentInAsync(_customer, amount, Now));
    }

    [Fact]
    public async Task PurchaseWithLines_LinesAttachToDocument()
    {
        var doc = await _service.RecordPurchaseAsync(_supplier, 120m, paidNow: false, Now, lines:
        [
            new DocumentLine { Description = "Shampoo 400ml", Quantity = 10, UnitPrice = 7m },
            new DocumentLine { Description = "Soap bars", Quantity = 25, UnitPrice = 2m },
        ]);

        var saved = await _store.GetDocumentAsync(doc.Id);
        Assert.NotNull(saved);
        Assert.Equal(2, saved.Lines.Count);
        Assert.All(saved.Lines, l => Assert.Equal(doc.Id, l.DocumentId));
        Assert.Equal(120m, saved.Lines.Sum(l => l.Total));
    }

    [Fact]
    public async Task Balances_ListShowsEachPartyNet()
    {
        await _service.RecordPurchaseAsync(_supplier, 500m, paidNow: false, Now);
        await _service.RecordSaleAsync(_customer, 60m, paidNow: false, Now);

        var balances = await _store.GetBalancesAsync();
        Assert.Equal(-500m, balances[_supplier]);
        Assert.Equal(60m, balances[_customer]);
    }
}
