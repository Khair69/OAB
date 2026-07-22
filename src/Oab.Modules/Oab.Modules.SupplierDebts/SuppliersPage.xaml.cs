using System.Globalization;
using Oab.App.Localization;
using Oab.App.Views;
using Oab.Core.Domain;

namespace Oab.Modules.SupplierDebts;

public partial class SuppliersPage : ContentPage
{
    private readonly SuppliersViewModel _viewModel;

    public SuppliersPage(SuppliersViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }

    private async void OnAddSupplierClicked(object? sender, EventArgs e)
    {
        var loc = LocalizationManager.Current!;
        var name = await DisplayPromptAsync(
            loc["Suppliers_AddSupplier"], loc["Suppliers_NamePrompt"],
            loc["Common_Save"], loc["Common_Cancel"]);
        if (!string.IsNullOrWhiteSpace(name))
            await _viewModel.AddSupplierAsync(name);
    }

    /// <summary>Tapping the card anywhere but a button opens that supplier's statement.</summary>
    private async void OnSupplierTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is SupplierRow row)
            await PartyStatementPage.PushAsync(Navigation, row.Party.Id, PartyRole.Supplier);
    }

    private async void OnRecordPaymentClicked(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not SupplierRow row)
            return;

        var loc = LocalizationManager.Current!;
        var text = await DisplayPromptAsync(
            loc["Suppliers_RecordPayment"], loc["Suppliers_PaymentPrompt"],
            loc["Common_Save"], loc["Common_Cancel"], keyboard: Keyboard.Numeric);
        if (string.IsNullOrWhiteSpace(text))
            return;

        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out var amount)
            || decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out amount))
        {
            await _viewModel.RecordPaymentAsync(row, amount);
        }
        else
        {
            await DisplayAlertAsync(loc["Common_Error"], loc["Common_InvalidAmount"], loc["Common_OK"]);
        }
    }
}
