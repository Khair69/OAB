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
}
