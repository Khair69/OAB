using Microsoft.EntityFrameworkCore;
using Oab.Core.Domain;
using Oab.Core.Ledger;

namespace Oab.Data;

/// <summary>
/// SQLite-backed ILedgerStore. Uses a context factory (a context per
/// operation) so it is safe as a singleton behind MAUI pages. Honors the
/// append-only rule: ledger entries are inserted, never updated or removed.
/// </summary>
public class LedgerStore(IDbContextFactory<OabDbContext> contextFactory) : ILedgerStore
{
    public async Task AddPartyAsync(Party party, CancellationToken ct = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        db.Parties.Add(party);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdatePartyAsync(Party party, CancellationToken ct = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        db.Parties.Update(party);
        await db.SaveChangesAsync(ct);
    }

    public async Task<Party?> GetPartyAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        return await db.Parties.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<IReadOnlyList<Party>> GetPartiesAsync(bool includeArchived = false, CancellationToken ct = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        return await db.Parties.AsNoTracking()
            .Where(p => includeArchived || !p.IsArchived)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
    }

    public async Task AddDocumentAsync(Document document, CancellationToken ct = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        db.Documents.Add(document);
        await db.SaveChangesAsync(ct);
    }

    public async Task<Document?> GetDocumentAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        return await db.Documents.AsNoTracking()
            .Include(d => d.Lines)
            .FirstOrDefaultAsync(d => d.Id == id, ct);
    }

    public async Task<IReadOnlyList<Document>> GetDocumentsAsync(DocumentKind? kind = null, Guid? partyId = null, CancellationToken ct = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        return await db.Documents.AsNoTracking()
            .Include(d => d.Lines)
            .Where(d => (kind == null || d.Kind == kind) && (partyId == null || d.PartyId == partyId))
            .OrderByDescending(d => d.OccurredAt)
            .ToListAsync(ct);
    }

    public async Task AddEntriesAsync(IReadOnlyList<LedgerEntry> entries, CancellationToken ct = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        db.LedgerEntries.AddRange(entries);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<LedgerEntry>> GetEntriesForPartyAsync(Guid partyId, CancellationToken ct = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        return await db.LedgerEntries.AsNoTracking()
            .Where(e => e.PartyId == partyId)
            .OrderByDescending(e => e.OccurredAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<LedgerEntry>> GetEntriesForDocumentAsync(Guid documentId, CancellationToken ct = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        return await db.LedgerEntries.AsNoTracking()
            .Where(e => e.DocumentId == documentId)
            .ToListAsync(ct);
    }

    public async Task<decimal> GetPartyBalanceAsync(Guid partyId, CancellationToken ct = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        // SQLite cannot SUM decimals server-side; pull the amounts and sum locally.
        var amounts = await db.LedgerEntries.AsNoTracking()
            .Where(e => e.PartyId == partyId)
            .Select(e => e.Amount)
            .ToListAsync(ct);
        return amounts.Sum();
    }

    public async Task<IReadOnlyDictionary<Guid, decimal>> GetBalancesAsync(CancellationToken ct = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        var pairs = await db.LedgerEntries.AsNoTracking()
            .Select(e => new { e.PartyId, e.Amount })
            .ToListAsync(ct);
        return pairs
            .GroupBy(p => p.PartyId)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));
    }
}
