using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Oab.App.Localization;
using Oab.Core.Domain;
using Oab.Core.Ledger;

namespace Oab.Modules.Purchases;

public partial class NewPurchaseViewModel(
    ILedgerStore store,
    LedgerService ledger,
    LocalizationManager localization) : ObservableObject
{
    public ObservableCollection<Party> Suppliers { get; } = [];

    [ObservableProperty]
    public partial Party? SelectedSupplier { get; set; }

    [ObservableProperty]
    public partial string NewSupplierName { get; set; } = "";

    [ObservableProperty]
    public partial string AmountText { get; set; } = "";

    [ObservableProperty]
    public partial bool PaidNow { get; set; }

    [ObservableProperty]
    public partial string Note { get; set; } = "";

    [ObservableProperty]
    public partial DateTime Date { get; set; } = DateTime.Today;

    [ObservableProperty]
    public partial string Error { get; set; } = "";

    public async Task LoadAsync()
    {
        Suppliers.Clear();
        foreach (var party in await store.GetPartiesAsync())
            Suppliers.Add(party);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        Error = "";

        if (!TryParseAmount(AmountText, out var amount) || amount <= 0m)
        {
            Error = localization["Common_InvalidAmount"];
            return;
        }

        Party? supplier = SelectedSupplier;
        var newName = NewSupplierName.Trim();
        if (newName.Length > 0)
        {
            supplier = new Party { Name = newName };
            await store.AddPartyAsync(supplier);
        }
        if (supplier is null)
        {
            Error = localization["Purchases_SelectSupplier"];
            return;
        }

        var occurredAt = new DateTimeOffset(Date.Date.Add(DateTime.Now.TimeOfDay),
            TimeZoneInfo.Local.GetUtcOffset(DateTime.Now));
        await ledger.RecordPurchaseAsync(supplier.Id, amount, PaidNow, occurredAt,
            Note.Trim().Length > 0 ? Note.Trim() : null);

        await Shell.Current.Navigation.PopAsync();
    }

    private static bool TryParseAmount(string text, out decimal amount) =>
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out amount)
        || decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
}
