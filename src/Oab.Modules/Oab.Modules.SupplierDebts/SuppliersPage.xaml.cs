using System.Globalization;
using Oab.App.Localization;

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
