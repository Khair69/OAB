using Oab.App.Diagnostics;

namespace Oab.Modules.Purchases;

public partial class NewPurchasePage : ContentPage
{
    private readonly NewPurchaseViewModel _viewModel;

    public NewPurchasePage(NewPurchaseViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await this.RunSafelyAsync(() => _viewModel.LoadAsync());
    }
}
