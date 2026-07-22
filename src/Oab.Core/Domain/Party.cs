namespace Oab.Core.Domain;

/// <summary>
/// Anyone the shop exchanges money with. In the souk the same person can be
/// a supplier one day and a customer the next, so there is a single Party
/// type instead of separate Supplier/Customer entities. What they "are" is
/// derived from the ledger, not declared here.
/// </summary>
public class Party
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string Name { get; set; }

    public string? Phone { get; set; }

    public string? Note { get; set; }

    /// <summary>
    /// Which lists this party appears in. A UI hint, not a source of truth —
    /// balances ignore it. <see cref="PartyRole.None"/> shows everywhere.
    /// </summary>
    public PartyRole Roles { get; set; }

    /// <summary>Hidden from pickers/lists but kept for history. Parties are never deleted.</summary>
    public bool IsArchived { get; set; }
}
