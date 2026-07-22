using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Oab.App.Diagnostics;
using Oab.App.Localization;
using Oab.Core.Domain;
using Oab.Core.Formatting;
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
        foreach (var party in await store.GetPartiesAsync(role: PartyRole.Supplier))
            Suppliers.Add(party);
    }

    /// <summary>
    /// The only save button in the app, and the one place a purchase is written.
    /// <para>
    /// A <c>[RelayCommand]</c> is not an event handler, so it never reached the
    /// pages' <c>RunSafelyAsync</c> — and <c>AsyncRelayCommand</c> rethrows a
    /// faulted body onto the synchronization context, which means a write failure
    /// here used to take the app down while the shopkeeper was looking at a form
    /// full of typing. It now reports into the same <c>Error</c> label that
    /// validation uses, so the form stays open and nothing is retyped.
    /// </para>
    /// </summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        Error = "";

        if (!MoneyInput.TryParseAmount(AmountText, out var amount) || amount <= 0m)
        {
            Error = localization["Common_InvalidAmount"];
            return;
        }

        Party? supplier = SelectedSupplier;
        var newName = NewSupplierName.Trim();
        if (supplier is null && newName.Length == 0)
        {
            Error = localization["Purchases_SelectSupplier"];
            return;
        }

        try
        {
            if (newName.Length > 0)
            {
                supplier = new Party { Name = newName, Roles = PartyRole.Supplier };
                await store.AddPartyAsync(supplier);
            }

            var occurredAt = new DateTimeOffset(Date.Date.Add(DateTime.Now.TimeOfDay),
                TimeZoneInfo.Local.GetUtcOffset(DateTime.Now));
            await ledger.RecordPurchaseAsync(supplier.Id, amount, PaidNow, occurredAt,
                Note.Trim().Length > 0 ? Note.Trim() : null);

            await Shell.Current.Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            // ErrorLog.Current rather than an injected instance: view models are
            // constructed directly in tests, and a logger that must be supplied
            // is a logger that gets left out. Null-safe by design.
            ErrorLog.Current?.Write($"{nameof(NewPurchaseViewModel)}.{nameof(SaveAsync)}", ex);
            Error = $"{localization["Common_Error"]}: {ex.Message}";
        }
    }
}
