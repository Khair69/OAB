using Oab.Core.Domain;

namespace Oab.Core.Ledger;

/// <summary>
/// Pure functions over ledger entries. Everything the UI shows about money
/// should come through here so the sign convention lives in exactly one place.
/// </summary>
public static class LedgerMath
{
    /// <summary>Positive: party owes the shop. Negative: shop owes the party.</summary>
    public static decimal Balance(IEnumerable<LedgerEntry> entries) =>
        entries.Sum(e => e.Amount);

    /// <summary>
    /// What is still unpaid on a document, as a positive number, given the
    /// entries that reference it. Zero means fully settled.
    /// </summary>
    public static decimal Outstanding(IEnumerable<LedgerEntry> documentEntries) =>
        Math.Abs(documentEntries.Sum(e => e.Amount));

    public static bool IsSettled(IEnumerable<LedgerEntry> documentEntries) =>
        documentEntries.Sum(e => e.Amount) == 0m;

    /// <summary>
    /// Converts a positive user-entered amount into the signed ledger amount
    /// for the given kind. Adjustments are entered pre-signed and rejected here.
    /// </summary>
    public static decimal SignedAmount(EntryKind kind, decimal positiveAmount)
    {
        if (positiveAmount <= 0m)
            throw new ArgumentOutOfRangeException(nameof(positiveAmount), "Amount must be positive.");

        return kind switch
        {
            EntryKind.Purchase => -positiveAmount,   // shop owes supplier
            EntryKind.PaymentOut => positiveAmount,  // shop settles what it owes
            EntryKind.Sale => positiveAmount,        // customer owes shop
            EntryKind.PaymentIn => -positiveAmount,  // customer settles what they owe
            EntryKind.Adjustment => throw new ArgumentException(
                "Adjustments carry their own sign; do not run them through SignedAmount.", nameof(kind)),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    /// <summary>
    /// The signed adjustment that makes an already-recorded entry count as
    /// <paramref name="correctedMagnitude"/> instead of what it says now — the
    /// arithmetic behind the correction flow.
    ///
    /// The shopkeeper only ever types a magnitude ("it should have been 100"),
    /// never a sign; the direction is taken from the entry being corrected,
    /// because a typo changes how much moved, never which way it moved. Feed the
    /// result straight to <c>LedgerService.RecordAdjustmentAsync</c>, which is
    /// the only method that accepts a pre-signed amount.
    ///
    /// <paramref name="correctedMagnitude"/> of zero is legal and means "this
    /// never happened": the returned delta cancels the entry out exactly, while
    /// leaving it on the record. Append-only has no delete.
    /// </summary>
    /// <returns>Zero when the entry already reads as the corrected amount — the
    /// caller should post nothing, since an adjustment of zero is rejected.</returns>
    public static decimal CorrectionDelta(decimal recordedAmount, decimal correctedMagnitude)
    {
        if (correctedMagnitude < 0m)
            throw new ArgumentOutOfRangeException(nameof(correctedMagnitude),
                "A corrected amount is a magnitude; direction comes from the entry being corrected.");
        if (recordedAmount == 0m)
            throw new ArgumentOutOfRangeException(nameof(recordedAmount),
                "An entry of zero has no direction to correct towards.");

        var corrected = recordedAmount < 0m ? -correctedMagnitude : correctedMagnitude;
        return corrected - recordedAmount;
    }
}
