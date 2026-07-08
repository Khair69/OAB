namespace Oab.Core.Domain;

public enum DocumentKind
{
    Purchase = 1,
    Sale = 2,
}

/// <summary>
/// Optional grouping of a transaction: "the crate of shampoo I bought Tuesday".
/// A document never carries money state itself — its debt and payments live in
/// the ledger entries that reference it, so "is it paid?" is always
/// SUM(entries where DocumentId == Id) == 0.
/// Line items are optional; shops that track only money leave them empty.
/// </summary>
public class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PartyId { get; set; }

    public DocumentKind Kind { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>Supplier's invoice number, if the shopkeeper wants to record it.</summary>
    public string? Number { get; set; }

    public string? Note { get; set; }

    public List<DocumentLine> Lines { get; set; } = [];
}

public class DocumentLine
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DocumentId { get; set; }

    public string Description { get; set; } = "";

    public decimal Quantity { get; set; } = 1;

    public decimal UnitPrice { get; set; }

    public decimal Total => Quantity * UnitPrice;
}
