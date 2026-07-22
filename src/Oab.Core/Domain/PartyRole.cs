namespace Oab.Core.Domain;

/// <summary>
/// How the shop treats a party, for UI grouping only — it never affects
/// balances (those are always derived from the ledger). A party can be both.
/// <see cref="None"/> means "unclassified": such parties show in every list,
/// which is the safe default for legacy rows created before roles existed.
/// </summary>
[Flags]
public enum PartyRole
{
    None = 0,
    Supplier = 1,
    Customer = 2,
}

public static class PartyRoleExtensions
{
    /// <summary>
    /// True if a party with <paramref name="roles"/> belongs in a list filtered
    /// to <paramref name="wanted"/>. Untagged parties (None) match everything.
    /// </summary>
    public static bool MatchesFilter(this PartyRole roles, PartyRole wanted) =>
        roles == PartyRole.None || (roles & wanted) != 0;
}
