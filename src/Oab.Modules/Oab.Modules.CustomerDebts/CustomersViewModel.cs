using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Oab.App.Formatting;
using Oab.App.Localization;
using Oab.Core.Domain;
using Oab.Core.Ledger;

namespace Oab.Modules.CustomerDebts;

public class CustomerRow
{
    public required Party Party { get; init; }
    public required string BalanceText { get; init; }
    public required Color BalanceColor { get; init; }
    /// <summary>The customer owes the shop (positive balance) — a debt to collect.</summary>
    public required bool OwesYou { get; init; }
}

public partial class CustomersViewModel(
    ILedgerStore store,
    LedgerService ledger,
    IMoneyFormatter money,
    LocalizationManager localization) : ObservableObject
{
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    public ObservableCollection<CustomerRow> Customers { get; } = [];

    public async Task LoadAsync()
    {
        if (IsBusy)
            return;
        IsBusy = true;
        try
        {
            var parties = await store.GetPartiesAsync(role: PartyRole.Customer);
            var balances = await store.GetBalancesAsync();

            Customers.Clear();
            foreach (var party in parties)
            {
                var balance = balances.GetValueOrDefault(party.Id);
                // Positive balance = customer owes the shop (opposite of a supplier).
                var (text, color) = balance switch
                {
                    > 0m => ($"{localization["Customers_OwesYou"]}: {money.Format(balance)}", Colors.Firebrick),
                    < 0m => ($"{localization["Customers_YouOwe"]}: {money.Format(balance)}", Colors.SeaGreen),
                    _ => (localization["Customers_Settled"], Colors.Gray),
                };
                Customers.Add(new CustomerRow
                {
                    Party = party,
                    BalanceText = text,
                    BalanceColor = color,
                    OwesYou = balance > 0m,
                });
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task AddCustomerAsync(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
            return;
        await store.AddPartyAsync(new Party { Name = trimmed, Roles = PartyRole.Customer });
        await LoadAsync();
    }

    /// <summary>Customer took goods on credit — records it as a sale on the tab.</summary>
    public async Task RecordDebtAsync(CustomerRow row, decimal amount)
    {
        if (amount <= 0m)
            return;
        await ledger.RecordSaleAsync(row.Party.Id, amount, paidNow: false, DateTimeOffset.Now);
        await LoadAsync();
    }

    /// <summary>Customer paid back some or all of what they owe.</summary>
    public async Task RecordPaymentAsync(CustomerRow row, decimal amount)
    {
        if (amount <= 0m)
            return;
        await ledger.RecordPaymentInAsync(row.Party.Id, amount, DateTimeOffset.Now);
        await LoadAsync();
    }
}
