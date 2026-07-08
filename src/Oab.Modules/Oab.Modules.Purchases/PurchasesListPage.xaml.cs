using Oab.App;

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
        await _viewModel.LoadAsync();
    }

    private async void OnNewPurchaseClicked(object? sender, EventArgs e) =>
        await Navigation.PushAsync(OabServices.Get<NewPurchasePage>());
}
