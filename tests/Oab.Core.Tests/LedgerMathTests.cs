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

    [Theory]
    // A purchase of 1000 that should have been 100: the shop owes 900 less.
    [InlineData(-1000, 100, 900)]
    // ...and one that should have been 1200: it owes 200 more.
    [InlineData(-1000, 1200, -200)]
    // Same arithmetic mirrored for a sale, where the customer owes the shop.
    [InlineData(1000, 100, -900)]
    [InlineData(1000, 1200, 200)]
    // Zero means the entry should never have existed; the delta cancels it out.
    [InlineData(-1000, 0, 1000)]
    [InlineData(1000, 0, -1000)]
    // Already right — the caller must post nothing, not an adjustment of zero.
    [InlineData(-1000, 1000, 0)]
    public void CorrectionDelta_MovesTheBalanceToWhatItShouldHaveBeen(
        decimal recorded, decimal corrected, decimal expected) =>
        Assert.Equal(expected, LedgerMath.CorrectionDelta(recorded, corrected));

    [Fact]
    public void CorrectionDelta_AppliedToTheEntry_LeavesExactlyTheCorrectedAmount()
    {
        // The property that matters: recorded + delta == the intended entry.
        LedgerEntry[] afterCorrection =
        [
            new() { Amount = -1000m },
            new() { Amount = LedgerMath.CorrectionDelta(-1000m, 100m) },
        ];

        Assert.Equal(-100m, LedgerMath.Balance(afterCorrection));
    }

    [Fact]
    public void CorrectionDelta_RejectsASignedCorrection() =>
        // Direction belongs to the entry being corrected, never to the typist.
        Assert.Throws<ArgumentOutOfRangeException>(() => LedgerMath.CorrectionDelta(-100m, -50m));

    [Fact]
    public void CorrectionDelta_RejectsAnEntryWithNoDirection() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => LedgerMath.CorrectionDelta(0m, 50m));
}
