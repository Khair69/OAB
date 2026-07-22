using System.Globalization;
using Oab.App.Localization;
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

    private static LocalizationManager Loc => LocalizationManager.Current!;

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
        await RunAsync(() => _viewModel.LoadAsync(_partyId, _perspective));
    }

    private async void OnEntryTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is StatementRow row)
            await RunAsync(() => CorrectAsync(row));
    }

    /// <summary>
    /// Three dialogs, each of which can be backed out of: what do you want to do
    /// with this row, what should the amount have been, and why. The middle one
    /// asks for a plain magnitude — the shopkeeper never sees or types a sign —
    /// and the last is pre-filled, so the common case is read, type, tap.
    /// </summary>
    private async Task CorrectAsync(StatementRow row)
    {
        // The sheet exists to name the action. Nothing on the statement says a
        // row is tappable, so this is where the feature is discovered; it is
        // also what makes a stray tap harmless.
        var chosen = await DisplayActionSheetAsync(
            Loc["Correct_Title"], Loc["Common_Cancel"], null, Loc["Correct_Action"]);
        if (chosen != Loc["Correct_Action"])
            return;

        var amountText = await DisplayPromptAsync(
            Loc["Correct_Title"], _viewModel.CorrectionPromptFor(row),
            Loc["Common_Save"], Loc["Common_Cancel"], keyboard: Keyboard.Numeric);
        if (amountText is null)
            return;
        if (!TryParseAmount(amountText, out var amount))
        {
            await DisplayAlertAsync(Loc["Common_Error"], Loc["Correct_InvalidAmount"], Loc["Common_OK"]);
            return;
        }

        var note = await DisplayPromptAsync(
            Loc["Correct_Title"], Loc["Correct_NotePrompt"],
            Loc["Common_Save"], Loc["Common_Cancel"],
            initialValue: _viewModel.SuggestedNoteFor(row));
        if (note is null)
            return;

        var outcome = await _viewModel.CorrectAsync(row, amount, note);
        var message = outcome switch
        {
            CorrectionOutcome.AlreadyThatAmount => Loc["Correct_Unchanged"],
            CorrectionOutcome.InvalidAmount => Loc["Correct_InvalidAmount"],
            CorrectionOutcome.NoteMissing => Loc["Correct_NoteRequired"],
            // Applied needs no message: the statement has already reloaded with
            // the gold-outlined correction on top and a new balance in the header.
            _ => null,
        };
        if (message is not null)
            await DisplayAlertAsync(Loc["Correct_Title"], message, Loc["Common_OK"]);
    }

    /// <summary>
    /// Same two-culture attempt as the other amount inputs. Neither pass reads
    /// Arabic-Indic digits yet — this is the fourth call site the fix has to
    /// cover ([10 §4](../../docs/10-status.md)).
    /// </summary>
    private static bool TryParseAmount(string text, out decimal amount) =>
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out amount)
        || decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);

    /// <summary>
    /// Event handlers are async void, so an escaping exception takes the whole
    /// app down with no message. Everything the user can trigger here funnels
    /// through this, the same as <c>BackupPage</c> does. Correcting money is the
    /// last place a silent crash is acceptable.
    /// </summary>
    private async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync(Loc["Common_Error"], ex.Message, Loc["Common_OK"]);
        }
    }
}
