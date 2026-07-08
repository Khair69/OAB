using Oab.Core.Domain;

namespace Oab.Core.Ledger;

/// <summary>
/// The use-case layer of the money engine: every way money can move gets a
/// method here, and each method only ever appends ledger entries. UI modules
/// call this; they never construct LedgerEntry rows themselves.
/// </summary>
public class LedgerService(ILedgerStore store)
{
    /// <summary>
    /// Records buying goods from a supplier. Always posts the debt entry; when
    /// <paramref name="paidNow"/> the settling payment is posted in the same
    /// document, so a cash purchase is simply a purchase that is already settled.
    /// </summary>
    public async Task<Document> RecordPurchaseAsync(
        Guid supplierId,
        decimal amount,
        bool paidNow,
        DateTimeOffset occurredAt,
        string? note = null,
        IEnumerable<DocumentLine>? lines = null,
        string? documentNumber = null,
        CancellationToken ct = default)
    {
        var document = new Document
        {
            PartyId = supplierId,
            Kind = DocumentKind.Purchase,
            OccurredAt = occurredAt,
            Number = documentNumber,
            Note = note,
        };
        if (lines is not null)
        {
            foreach (var line in lines)
            {
                line.DocumentId = document.Id;
                document.Lines.Add(line);
            }
        }

        var entries = new List<LedgerEntry>
        {
            NewEntry(supplierId, document.Id, occurredAt, EntryKind.Purchase,
                LedgerMath.SignedAmount(EntryKind.Purchase, amount), note),
        };
        if (paidNow)
        {
            entries.Add(NewEntry(supplierId, document.Id, occurredAt, EntryKind.PaymentOut,
                LedgerMath.SignedAmount(EntryKind.PaymentOut, amount), note));
        }

        await store.AddDocumentAsync(document, ct);
        await store.AddEntriesAsync(entries, ct);
        return document;
    }

    /// <summary>Records selling goods to a customer; mirror image of a purchase.</summary>
    public async Task<Document> RecordSaleAsync(
        Guid customerId,
        decimal amount,
        bool paidNow,
        DateTimeOffset occurredAt,
        string? note = null,
        IEnumerable<DocumentLine>? lines = null,
        string? documentNumber = null,
        CancellationToken ct = default)
    {
        var document = new Document
        {
            PartyId = customerId,
            Kind = DocumentKind.Sale,
            OccurredAt = occurredAt,
            Number = documentNumber,
            Note = note,
        };
        if (lines is not null)
        {
            foreach (var line in lines)
            {
                line.DocumentId = document.Id;
                document.Lines.Add(line);
            }
        }

        var entries = new List<LedgerEntry>
        {
            NewEntry(customerId, document.Id, occurredAt, EntryKind.Sale,
                LedgerMath.SignedAmount(EntryKind.Sale, amount), note),
        };
        if (paidNow)
        {
            entries.Add(NewEntry(customerId, document.Id, occurredAt, EntryKind.PaymentIn,
                LedgerMath.SignedAmount(EntryKind.PaymentIn, amount), note));
        }

        await store.AddDocumentAsync(document, ct);
        await store.AddEntriesAsync(entries, ct);
        return document;
    }

    /// <summary>
    /// Shop pays a party (usually a supplier). Optionally tied to a specific
    /// document so that invoice shows as (partially) paid.
    /// </summary>
    public async Task<LedgerEntry> RecordPaymentOutAsync(
        Guid partyId,
        decimal amount,
        DateTimeOffset occurredAt,
        Guid? documentId = null,
        string? note = null,
        CancellationToken ct = default)
    {
        var entry = NewEntry(partyId, documentId, occurredAt, EntryKind.PaymentOut,
            LedgerMath.SignedAmount(EntryKind.PaymentOut, amount), note);
        await store.AddEntriesAsync([entry], ct);
        return entry;
    }

    /// <summary>A party pays the shop (usually a customer settling their debt).</summary>
    public async Task<LedgerEntry> RecordPaymentInAsync(
        Guid partyId,
        decimal amount,
        DateTimeOffset occurredAt,
        Guid? documentId = null,
        string? note = null,
        CancellationToken ct = default)
    {
        var entry = NewEntry(partyId, documentId, occurredAt, EntryKind.PaymentIn,
            LedgerMath.SignedAmount(EntryKind.PaymentIn, amount), note);
        await store.AddEntriesAsync([entry], ct);
        return entry;
    }

    /// <summary>
    /// Manual correction, entered pre-signed (positive = party owes shop more).
    /// This is how mistakes are fixed — history is never edited.
    /// </summary>
    public async Task<LedgerEntry> RecordAdjustmentAsync(
        Guid partyId,
        decimal signedAmount,
        DateTimeOffset occurredAt,
        string note,
        Guid? documentId = null,
        CancellationToken ct = default)
    {
        if (signedAmount == 0m)
            throw new ArgumentOutOfRangeException(nameof(signedAmount), "An adjustment of zero changes nothing.");
        if (string.IsNullOrWhiteSpace(note))
            throw new ArgumentException("Adjustments must say why.", nameof(note));

        var entry = NewEntry(partyId, documentId, occurredAt, EntryKind.Adjustment, signedAmount, note);
        await store.AddEntriesAsync([entry], ct);
        return entry;
    }

    public Task<decimal> GetPartyBalanceAsync(Guid partyId, CancellationToken ct = default) =>
        store.GetPartyBalanceAsync(partyId, ct);

    /// <summary>Unpaid remainder of a document, as a positive number. Zero = settled.</summary>
    public async Task<decimal> GetDocumentOutstandingAsync(Guid documentId, CancellationToken ct = default) =>
        LedgerMath.Outstanding(await store.GetEntriesForDocumentAsync(documentId, ct));

    private static LedgerEntry NewEntry(
        Guid partyId, Guid? documentId, DateTimeOffset occurredAt,
        EntryKind kind, decimal signedAmount, string? note) => new()
    {
        PartyId = partyId,
        DocumentId = documentId,
        OccurredAt = occurredAt,
        Kind = kind,
        Amount = signedAmount,
        Note = note,
    };
}
