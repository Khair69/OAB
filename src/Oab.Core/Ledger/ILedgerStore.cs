using Oab.Core.Domain;

namespace Oab.Core.Ledger;

/// <summary>
/// Persistence boundary for the money engine. Implemented over SQLite in
/// Oab.Data; tests use an in-memory fake. Note there is no update/delete for
/// entries — the ledger is append-only by construction.
/// </summary>
public interface ILedgerStore
{
    Task AddPartyAsync(Party party, CancellationToken ct = default);
    Task UpdatePartyAsync(Party party, CancellationToken ct = default);
    Task<Party?> GetPartyAsync(Guid id, CancellationToken ct = default);
    /// <summary>
    /// Parties for a list/picker. When <paramref name="role"/> is given, only
    /// parties carrying that role are returned — plus untagged
    /// (<see cref="PartyRole.None"/>) parties, which belong to every list.
    /// </summary>
    Task<IReadOnlyList<Party>> GetPartiesAsync(bool includeArchived = false, PartyRole? role = null, CancellationToken ct = default);

    Task AddDocumentAsync(Document document, CancellationToken ct = default);
    Task<Document?> GetDocumentAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Document>> GetDocumentsAsync(DocumentKind? kind = null, Guid? partyId = null, CancellationToken ct = default);

    Task AddEntriesAsync(IReadOnlyList<LedgerEntry> entries, CancellationToken ct = default);
    Task<IReadOnlyList<LedgerEntry>> GetEntriesForPartyAsync(Guid partyId, CancellationToken ct = default);
    Task<IReadOnlyList<LedgerEntry>> GetEntriesForDocumentAsync(Guid documentId, CancellationToken ct = default);
    /// <summary>
    /// Entries for many documents at once, keyed by document id. Documents with
    /// no entries are simply absent — a caller asking about an unknown id gets
    /// nothing back, not an empty-list entry.
    /// <para>
    /// This exists because a list screen needs "is each of these invoices paid?"
    /// and asking one invoice at a time is a query per row. Same shape, and same
    /// reason, as <see cref="GetBalancesAsync"/>.
    /// </para>
    /// </summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<LedgerEntry>>> GetEntriesForDocumentsAsync(
        IReadOnlyCollection<Guid> documentIds, CancellationToken ct = default);

    Task<decimal> GetPartyBalanceAsync(Guid partyId, CancellationToken ct = default);
    /// <summary>Balance per party id, for list screens. Parties with no entries may be absent.</summary>
    Task<IReadOnlyDictionary<Guid, decimal>> GetBalancesAsync(CancellationToken ct = default);
}
