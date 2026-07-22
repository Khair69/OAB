using Oab.Core.Domain;

namespace Oab.App.Views;

/// <summary>
/// Shared detail page — not a module feature. Any list that shows a party can
/// push it, which is why it lives in the shell rather than in SupplierDebts or
/// CustomerDebts (a party is often both).
/// </summary>
public partial class PartyStatementPage : ContentPage
{
    private readonly PartyStatementViewModel _viewModel;
    private Guid _partyId;
    private PartyRole _perspective;

    public PartyStatementPage(PartyStatementViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    /// <summary>
    /// How every list screen opens a statement. Kept here so modules need to
    /// know nothing about how the page is built or how it receives the party.
    /// <paramref name="perspective"/> is the list doing the pushing — it only
    /// decides which direction of debt gets the alarm colour, so the balance
    /// looks the same here as it did on the row that was tapped.
    /// </summary>
    public static Task PushAsync(INavigation navigation, Guid partyId, PartyRole perspective = PartyRole.None)
    {
        var page = OabServices.Get<PartyStatementPage>();
        page._partyId = partyId;
        page._perspective = perspective;
        return navigation.PushAsync(page);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync(_partyId, _perspective);
    }
}
