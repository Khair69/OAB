using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Oab.App.Formatting;
using Oab.App.Localization;
using Oab.Core.Domain;
using Oab.Core.Ledger;

namespace Oab.App.Views;

/// <summary>
/// One line of the statement: an entry, and the balance it left behind. Text is
/// rendered here rather than in XAML so the page needs no value converters.
/// </summary>
public sealed class StatementRow
{
    public required LedgerEntry Entry { get; init; }
    public required string DateText { get; init; }
    public required string KindText { get; init; }
    public required string AmountText { get; init; }
    /// <summary>The party's balance immediately after this entry, phrased in words.</summary>
    public required string BalanceAfterText { get; init; }
    public required string NoteText { get; init; }
    public required bool HasNote { get; init; }
    /// <summary>An Adjustment. Outlined on screen so a corrected history reads as corrected.</summary>
    public required bool IsCorrection { get; init; }
}

/// <summary>
/// The notebook page for one party: every entry that touched their balance, in
/// order, with the balance after each one. This is what makes a number
/// explainable — "why do I owe 500?" is answered by scrolling.
/// </summary>
public partial class PartyStatementViewModel(
    ILedgerStore store,
    IMoneyFormatter money,
    LocalizationManager localization) : ObservableObject
{
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string PartyName { get; set; } = string.Empty;

    /// <summary>Where the party stands right now — the same text as the last row's balance.</summary>
    [ObservableProperty]
    public partial string BalanceText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial Color BalanceColor { get; set; } = Colors.Gray;

    public ObservableCollection<StatementRow> Rows { get; } = [];

    /// <summary>
    /// Which list the shopkeeper tapped in from. It decides exactly one thing:
    /// which direction of debt is the expected one, and so gets the alarm
    /// colour. Both lists already mean the same thing by red — "the debt this
    /// screen is about is still open" — they just point opposite ways, and this
    /// page is shared, so the caller says which way. A party tagged as both, or
    /// as neither, has no expected direction and stays neutral.
    /// </summary>
    private PartyRole _perspective;

    public async Task LoadAsync(Guid partyId, PartyRole perspective = PartyRole.None)
    {
        if (IsBusy)
            return;
        IsBusy = true;
        _perspective = perspective;
        try
        {
            var party = await store.GetPartyAsync(partyId);
            PartyName = party?.Name ?? string.Empty;

            var entries = await store.GetEntriesForPartyAsync(partyId);
            // Sorted here rather than trusting the store: a running balance only
            // means anything in the order the money actually moved. CreatedAtUtc
            // breaks ties between entries stamped with the same OccurredAt.
            var chronological = entries
                .OrderBy(e => e.OccurredAt)
                .ThenBy(e => e.CreatedAtUtc)
                .ToList();

            var running = 0m;
            var rows = new List<StatementRow>(chronological.Count);
            foreach (var entry in chronological)
            {
                running += entry.Amount;
                rows.Add(new StatementRow
                {
                    Entry = entry,
                    DateText = entry.OccurredAt.ToString("d", localization.Culture),
                    KindText = KindLabel(entry.Kind),
                    AmountText = money.Format(entry.Amount),
                    BalanceAfterText = Describe(running),
                    NoteText = entry.Note ?? string.Empty,
                    HasNote = !string.IsNullOrWhiteSpace(entry.Note),
                    IsCorrection = entry.Kind == EntryKind.Adjustment,
                });
            }

            BalanceText = Describe(running);
            BalanceColor = ColorFor(running);

            Rows.Clear();
            // Newest first on screen. The shopkeeper opened this to ask "why is
            // this number what it is?", and the answer is usually the last few
            // lines — not the ones from three months ago.
            for (var i = rows.Count - 1; i >= 0; i--)
                Rows.Add(rows[i]);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Direction in words, magnitude in digits — the same contract as
    /// <see cref="Core.Formatting.MoneyFormat"/>. The words carry the meaning on
    /// their own; the colour beside them is only an accelerant.
    /// </summary>
    private string Describe(decimal balance) => balance switch
    {
        < 0m => $"{localization["Statement_YouOwe"]}: {money.Format(balance)}",
        > 0m => $"{localization["Statement_TheyOwe"]}: {money.Format(balance)}",
        _ => localization["Statement_Settled"],
    };

    /// <summary>
    /// Matches whichever list pushed this page, so the balance doesn't change
    /// colour as the shopkeeper taps into it. See <see cref="_perspective"/>.
    /// </summary>
    private Color ColorFor(decimal balance) => _perspective switch
    {
        // Negative = the shop owes them (see LedgerEntry's sign convention).
        PartyRole.Supplier => balance switch
        {
            < 0m => Colors.Firebrick,
            > 0m => Colors.SeaGreen,
            _ => Colors.Gray,
        },
        PartyRole.Customer => balance switch
        {
            > 0m => Colors.Firebrick,
            < 0m => Colors.SeaGreen,
            _ => Colors.Gray,
        },
        _ => Colors.Gray,
    };

    /// <summary>The kind carries the direction, which is why amounts show no sign.</summary>
    private string KindLabel(EntryKind kind)
    {
        var key = kind switch
        {
            EntryKind.Purchase => "Statement_KindPurchase",
            EntryKind.Sale => "Statement_KindSale",
            EntryKind.PaymentOut => "Statement_KindPaymentOut",
            EntryKind.PaymentIn => "Statement_KindPaymentIn",
            EntryKind.Adjustment => "Statement_KindAdjustment",
            _ => null,
        };
        // A kind added to Core before this screen learns about it still shows
        // something, rather than an empty row.
        return key is null ? kind.ToString() : localization[key];
    }
}
