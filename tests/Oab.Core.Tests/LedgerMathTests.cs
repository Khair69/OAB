using Oab.Core.Domain;
using Oab.Core.Ledger;

namespace Oab.Core.Tests;

public class LedgerMathTests
{
    [Theory]
    [InlineData(EntryKind.Purchase, 100, -100)]
    [InlineData(EntryKind.PaymentOut, 100, 100)]
    [InlineData(EntryKind.Sale, 100, 100)]
    [InlineData(EntryKind.PaymentIn, 100, -100)]
    public void SignedAmount_FollowsConvention(EntryKind kind, decimal input, decimal expected) =>
        Assert.Equal(expected, LedgerMath.SignedAmount(kind, input));

    [Fact]
    public void SignedAmount_RejectsAdjustments() =>
        Assert.Throws<ArgumentException>(() => LedgerMath.SignedAmount(EntryKind.Adjustment, 10m));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SignedAmount_RejectsNonPositive(decimal amount) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => LedgerMath.SignedAmount(EntryKind.Sale, amount));

    [Fact]
    public void Outstanding_IsPositive_ForBothPurchaseAndSaleDocuments()
    {
        LedgerEntry[] purchaseDoc = [new() { Amount = -250m }, new() { Amount = 100m }];
        LedgerEntry[] saleDoc = [new() { Amount = 250m }, new() { Amount = -100m }];

        Assert.Equal(150m, LedgerMath.Outstanding(purchaseDoc));
        Assert.Equal(150m, LedgerMath.Outstanding(saleDoc));
        Assert.False(LedgerMath.IsSettled(purchaseDoc));
    }

    [Fact]
    public void EmptyLedger_BalancesToZero()
    {
        Assert.Equal(0m, LedgerMath.Balance([]));
        Assert.True(LedgerMath.IsSettled([]));
    }
}
