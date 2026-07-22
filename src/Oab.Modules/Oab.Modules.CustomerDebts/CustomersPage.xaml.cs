using System.Globalization;
using Oab.App.Localization;
using Oab.App.Views;
using Oab.Core.Domain;

namespace Oab.Modules.CustomerDebts;

public partial class CustomersPage : ContentPage
{
    private readonly CustomersViewModel _viewModel;

    public CustomersPage(CustomersViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }

    private async void OnAddCustomerClicked(object? sender, EventArgs e)
    {
        var loc = LocalizationManager.Current!;
        var name = await DisplayPromptAsync(
            loc["Customers_AddCustomer"], loc["Customers_NamePrompt"],
            loc["Common_Save"], loc["Common_Cancel"]);
        if (!string.IsNullOrWhiteSpace(name))
            await _viewModel.AddCustomerAsync(name);
    }

    /// <summary>Tapping the card anywhere but a button opens that customer's statement.</summary>
    private async void OnCustomerTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is CustomerRow row)
            await PartyStatementPage.PushAsync(Navigation, row.Party.Id, PartyRole.Customer);
    }

    private async void OnRecordDebtClicked(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not CustomerRow row)
            return;

        var loc = LocalizationManager.Current!;
        var text = await DisplayPromptAsync(
            loc["Customers_RecordDebt"], loc["Customers_DebtPrompt"],
            loc["Common_Save"], loc["Common_Cancel"], keyboard: Keyboard.Numeric);
        await ApplyAmountAsync(text, amount => _viewModel.RecordDebtAsync(row, amount));
    }

    private async void OnCollectPaymentClicked(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not CustomerRow row)
            return;

        var loc = LocalizationManager.Current!;
        var text = await DisplayPromptAsync(
            loc["Customers_CollectPayment"], loc["Customers_PaymentPrompt"],
            loc["Common_Save"], loc["Common_Cancel"], keyboard: Keyboard.Numeric);
        await ApplyAmountAsync(text, amount => _viewModel.RecordPaymentAsync(row, amount));
    }

    private async Task ApplyAmountAsync(string? text, Func<decimal, Task> action)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out var amount)
            || decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out amount))
        {
            await action(amount);
        }
        else
        {
            var loc = LocalizationManager.Current!;
            await DisplayAlertAsync(loc["Common_Error"], loc["Common_InvalidAmount"], loc["Common_OK"]);
        }
    }
}
