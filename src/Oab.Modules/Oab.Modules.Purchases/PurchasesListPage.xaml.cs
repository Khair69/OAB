using Oab.App;
using Oab.App.Diagnostics;

namespace Oab.Modules.Purchases;

public partial class PurchasesListPage : ContentPage
{
    private readonly PurchasesListViewModel _viewModel;

    public PurchasesListPage(PurchasesListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await this.RunSafelyAsync(() => _viewModel.LoadAsync());
    }

    // Resolving the page from DI can throw — a module that forgot to register it
    // fails exactly here — and this is `async void`, so it needs the same funnel.
    private async void OnNewPurchaseClicked(object? sender, EventArgs e) =>
        await this.RunSafelyAsync(() => Navigation.PushAsync(OabServices.Get<NewPurchasePage>()));
}
