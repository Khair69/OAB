namespace Oab.Core.Domain;

public enum EntryKind
{
    /// <summary>Shop bought goods from the party. Increases what the shop owes them.</summary>
    Purchase = 1,

    /// <summary>Shop sold goods to the party. Increases what they owe the shop.</summary>
    Sale = 2,

    /// <summary>Shop paid money to the party (e.g. settling a supplier debt).</summary>
    PaymentOut = 3,

    /// <summary>Party paid money to the shop (e.g. settling a customer debt).</summary>
    PaymentIn = 4,

    /// <summary>Manual correction. The only kind whose amount is entered pre-signed.</summary>
    Adjustment = 5,
}
