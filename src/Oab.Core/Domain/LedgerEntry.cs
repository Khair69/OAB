namespace Oab.Core.Domain;

/// <summary>
/// One immutable line in the money ledger. Entries are only ever appended —
/// corrections are new <see cref="EntryKind.Adjustment"/> entries, never edits
/// or deletes. All balances are sums over these rows.
///
/// Sign convention for <see cref="Amount"/>:
///   positive = the party owes the shop; negative = the shop owes the party.
/// </summary>
public class LedgerEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PartyId { get; set; }

    /// <summary>Set when the entry belongs to a document (invoice/sale ticket) or settles one.</summary>
    public Guid? DocumentId { get; set; }

    /// <summary>When the transaction happened, as the shopkeeper saw it (local time preserved via offset).</summary>
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>When the row was recorded on the device. Basis for append-only ordering and future sync.</summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public decimal Amount { get; set; }

    public EntryKind Kind { get; set; }

    public string? Note { get; set; }
}
