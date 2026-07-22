using Oab.Core.Domain;
using Oab.Core.Ledger;

namespace Oab.Core.Tests;

/// <summary>In-memory ILedgerStore for exercising LedgerService without a database.</summary>
public class InMemoryLedgerStore : ILedgerStore
{
    private readonly Dictionary<Guid, Party> _parties = [];
    private readonly Dictionary<Guid, Document> _documents = [];
    private readonly List<LedgerEntry> _entries = [];

    public IReadOnlyList<LedgerEntry> Entries => _entries;

    public Task AddPartyAsync(Party party, CancellationToken ct = default)
    {
        _parties.Add(party.Id, party);
        return Task.CompletedTask;
    }

    public Task UpdatePartyAsync(Party party, CancellationToken ct = default)
    {
        _parties[party.Id] = party;
        return Task.CompletedTask;
    }

    public Task<Party?> GetPartyAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_parties.GetValueOrDefault(id));

    public Task<IReadOnlyList<Party>> GetPartiesAsync(bool includeArchived = false, PartyRole? role = null, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Party>>(_parties.Values
            .Where(p => includeArchived || !p.IsArchived)
            .Where(p => role is not PartyRole wanted || p.Roles.MatchesFilter(wanted))
            .ToList());

    public Task AddDocumentAsync(Document document, CancellationToken ct = default)
    {
        _documents.Add(document.Id, document);
        return Task.CompletedTask;
    }

    public Task<Document?> GetDocumentAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_documents.GetValueOrDefault(id));

    public Task<IReadOnlyList<Document>> GetDocumentsAsync(DocumentKind? kind = null, Guid? partyId = null, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Document>>(_documents.Values
            .Where(d => (kind is null || d.Kind == kind) && (partyId is null || d.PartyId == partyId))
            .ToList());

    public Task AddEntriesAsync(IReadOnlyList<LedgerEntry> entries, CancellationToken ct = default)
    {
        _entries.AddRange(entries);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<LedgerEntry>> GetEntriesForPartyAsync(Guid partyId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<LedgerEntry>>(_entries.Where(e => e.PartyId == partyId).ToList());

    public Task<IReadOnlyList<LedgerEntry>> GetEntriesForDocumentAsync(Guid documentId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<LedgerEntry>>(_entries.Where(e => e.DocumentId == documentId).ToList());

    public Task<decimal> GetPartyBalanceAsync(Guid partyId, CancellationToken ct = default) =>
        Task.FromResult(_entries.Where(e => e.PartyId == partyId).Sum(e => e.Amount));

    public Task<IReadOnlyDictionary<Guid, decimal>> GetBalancesAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, decimal>>(_entries
            .GroupBy(e => e.PartyId)
            .ToDictionary(g => g.Key, g => g.Sum(e => e.Amount)));
}
