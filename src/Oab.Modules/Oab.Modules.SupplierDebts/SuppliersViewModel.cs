using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Oab.App.Formatting;
using Oab.App.Localization;
using Oab.Core.Domain;
using Oab.Core.Ledger;

namespace Oab.Modules.SupplierDebts;

public class SupplierRow
{
    public required Party Party { get; init; }
    public required string BalanceText { get; init; }
    public required Color BalanceColor { get; init; }
    public required bool HasDebt { get; init; }
}

public partial class SuppliersViewModel(
    ILedgerStore store,
    LedgerService ledger,
    IMoneyFormatter money,
    LocalizationManager localization) : ObservableObject
{
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    public ObservableCollection<SupplierRow> Suppliers { get; } = [];

    public async Task LoadAsync()
    {
        if (IsBusy)
            return;
        IsBusy = true;
        try
        {
            var parties = await store.GetPartiesAsync();
            var balances = await store.GetBalancesAsync();

            Suppliers.Clear();
            foreach (var party in parties)
            {
                var balance = balances.GetValueOrDefault(party.Id);
                // Negative balance = the shop owes this party (see LedgerEntry sign convention).
                var (text, color) = balance switch
                {
                    < 0m => ($"{localization["Suppliers_YouOwe"]}: {money.Format(balance)}", Colors.Firebrick),
                    > 0m => ($"{localization["Suppliers_TheyOwe"]}: {money.Format(balance)}", Colors.SeaGreen),
                    _ => (localization["Suppliers_Settled"], Colors.Gray),
                };
                Suppliers.Add(new SupplierRow
                {
                    Party = party,
                    BalanceText = text,
                    BalanceColor = color,
                    HasDebt = balance < 0m,
                });
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task AddSupplierAsync(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
            return;
        await store.AddPartyAsync(new Party { Name = trimmed });
        await LoadAsync();
    }

    public async Task RecordPaymentAsync(SupplierRow row, decimal amount)
    {
        if (amount <= 0m)
            return;
        await ledger.RecordPaymentOutAsync(row.Party.Id, amount, DateTimeOffset.Now);
        await LoadAsync();
    }
}
