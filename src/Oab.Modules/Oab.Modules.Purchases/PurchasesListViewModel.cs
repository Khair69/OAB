using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Oab.App.Formatting;
using Oab.App.Localization;
using Oab.Core.Domain;
using Oab.Core.Ledger;

namespace Oab.Modules.Purchases;

public class PurchaseRow
{
    public required Document Document { get; init; }
    public required string SupplierName { get; init; }
    public required string TotalText { get; init; }
    public required string DateText { get; init; }
    public required string StatusText { get; init; }
    public required Color StatusColor { get; init; }
    public required bool IsUnpaid { get; init; }
    public required decimal Outstanding { get; init; }
}

public partial class PurchasesListViewModel(
    ILedgerStore store,
    LedgerService ledger,
    IMoneyFormatter money,
    LocalizationManager localization) : ObservableObject
{
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    public ObservableCollection<PurchaseRow> Purchases { get; } = [];

    public async Task LoadAsync()
    {
        if (IsBusy)
            return;
        IsBusy = true;
        try
        {
            var documents = await store.GetDocumentsAsync(DocumentKind.Purchase);
            var parties = (await store.GetPartiesAsync(includeArchived: true))
                .ToDictionary(p => p.Id, p => p.Name);
            // Three queries for the whole screen, not three plus one per row.
            // This loop used to ask the database about each invoice separately:
            // fine at ten purchases, 2,000 round trips at two thousand — on every
            // OnAppearing, on a cheap phone. Archived parties are still fetched
            // by name so a settled supplier's old invoices don't render as "?".
            var entriesByDocument = await store.GetEntriesForDocumentsAsync(
                [.. documents.Select(d => d.Id)]);

            Purchases.Clear();
            foreach (var doc in documents)
            {
                var entries = entriesByDocument.GetValueOrDefault(doc.Id, []);
                var total = Math.Abs(entries.Where(e => e.Kind == EntryKind.Purchase).Sum(e => e.Amount));
                var outstanding = LedgerMath.Outstanding(entries);
                var isUnpaid = outstanding > 0m;

                Purchases.Add(new PurchaseRow
                {
                    Document = doc,
                    SupplierName = parties.GetValueOrDefault(doc.PartyId, "?"),
                    TotalText = money.Format(total),
                    DateText = doc.OccurredAt.ToString("d", localization.Culture),
                    StatusText = isUnpaid
                        ? $"{localization["Common_Remaining"]}: {money.Format(outstanding)}"
                        : localization["Common_Paid"],
                    StatusColor = isUnpaid ? Colors.Firebrick : Colors.SeaGreen,
                    IsUnpaid = isUnpaid,
                    Outstanding = outstanding,
                });
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task PayRemainingAsync(PurchaseRow row)
    {
        if (!row.IsUnpaid)
            return;
        await ledger.RecordPaymentOutAsync(row.Document.PartyId, row.Outstanding,
            DateTimeOffset.Now, row.Document.Id);
        await LoadAsync();
    }
}
