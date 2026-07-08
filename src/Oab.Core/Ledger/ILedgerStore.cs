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
    Task<IReadOnlyList<Party>> GetPartiesAsync(bool includeArchived = false, CancellationToken ct = default);

    Task AddDocumentAsync(Document document, CancellationToken ct = default);
    Task<Document?> GetDocumentAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Document>> GetDocumentsAsync(DocumentKind? kind = null, Guid? partyId = null, CancellationToken ct = default);

    Task AddEntriesAsync(IReadOnlyList<LedgerEntry> entries, CancellationToken ct = default);
    Task<IReadOnlyList<LedgerEntry>> GetEntriesForPartyAsync(Guid partyId, CancellationToken ct = default);
    Task<IReadOnlyList<LedgerEntry>> GetEntriesForDocumentAsync(Guid documentId, CancellationToken ct = default);

    Task<decimal> GetPartyBalanceAsync(Guid partyId, CancellationToken ct = default);
    /// <summary>Balance per party id, for list screens. Parties with no entries may be absent.</summary>
    Task<IReadOnlyDictionary<Guid, decimal>> GetBalancesAsync(CancellationToken ct = default);
}
