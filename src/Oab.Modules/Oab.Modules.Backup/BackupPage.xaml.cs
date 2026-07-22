using System.Runtime.CompilerServices;
using Oab.App.Diagnostics;
using Oab.App.Localization;

namespace Oab.Modules.Backup;

public partial class BackupPage : ContentPage
{
    private readonly BackupViewModel _viewModel;

    public BackupPage(BackupViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    private static LocalizationManager Loc => LocalizationManager.Current!;

    /// <summary>
    /// Re-checks whether anything has been logged, because the card that offers
    /// to send the error log is hidden until something goes wrong — and something
    /// may have gone wrong on another screen since this page was last open.
    /// </summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.Refresh();
    }

    private async void OnShareDatabaseClicked(object? sender, EventArgs e) =>
        await RunAsync(async () =>
        {
            var path = await _viewModel.CreateDatabaseSnapshotAsync();
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = Loc["Backup_ShareTitle"],
                File = new ShareFile(path),
            });
        });

    private async void OnShareSummaryClicked(object? sender, EventArgs e) =>
        await RunAsync(async () =>
        {
            var path = await _viewModel.CreateSummaryFileAsync();
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = Loc["Backup_ShareTitle"],
                File = new ShareFile(path),
            });
        });

    /// <summary>
    /// The other half of "log to a shareable file": a log sitting in the app's
    /// private directory on a phone with no cable and no developer nearby is not
    /// evidence, it is a file. This puts it on the share sheet next to the
    /// backups, because that is the one screen a shopkeeper already knows sends
    /// things out of the app.
    /// </summary>
    private async void OnShareLogClicked(object? sender, EventArgs e) =>
        await RunAsync(async () =>
        {
            var path = await _viewModel.CreateErrorLogFileAsync();
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = Loc["Backup_ShareLog"],
                File = new ShareFile(path),
            });
        });

    private async void OnRestoreClicked(object? sender, EventArgs e)
    {
        // Restoring throws away the current book, so make the user say yes twice:
        // once to the warning, and once by deliberately choosing the file.
        var confirmed = await DisplayAlertAsync(
            Loc["Backup_Restore"], Loc["Backup_RestoreWarning"],
            Loc["Backup_RestoreConfirm"], Loc["Common_Cancel"]);
        if (!confirmed)
            return;

        var picked = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = Loc["Backup_Restore"],
        });
        if (picked is null)
            return;

        await RunAsync(async () =>
        {
            if (!await _viewModel.IsValidBackupAsync(picked.FullPath))
            {
                await DisplayAlertAsync(Loc["Common_Error"], Loc["Backup_RestoreInvalid"], Loc["Common_OK"]);
                return;
            }

            await _viewModel.RestoreAsync(picked.FullPath);
            _viewModel.Status = Loc["Backup_RestoreDone"];
            await DisplayAlertAsync(Loc["Backup_Title"], Loc["Backup_RestoreDone"], Loc["Common_OK"]);
        });
    }

    /// <summary>
    /// This page's own idiom on top of the shared
    /// <see cref="PageErrorHandling.RunSafelyAsync"/>: a busy guard, because
    /// every action here is slow enough to be tapped twice, and a failure
    /// reported into the status label rather than an alert, because an alert on
    /// top of a share sheet is a dialog nobody sees.
    /// <para>
    /// <paramref name="context"/> is forwarded explicitly. Without it every log
    /// record from this page would read <c>BackupPage.RunAsync</c> rather than
    /// naming the button that was pressed.
    /// </para>
    /// </summary>
    private async Task RunAsync(Func<Task> action, [CallerMemberName] string context = "")
    {
        if (_viewModel.IsBusy)
            return;

        _viewModel.IsBusy = true;
        _viewModel.Status = "";
        try
        {
            await this.RunSafelyAsync(
                action,
                onError: ex => _viewModel.Status = $"{Loc["Common_Error"]}: {ex.Message}",
                context: context);
        }
        finally
        {
            _viewModel.IsBusy = false;
            // A failure that just got logged is exactly when the log becomes
            // worth offering, so re-evaluate before the user leaves the screen.
            _viewModel.Refresh();
        }
    }
}
