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
    /// Event handlers are async void, so an escaping exception would crash the
    /// app rather than surface. Everything the user triggers here funnels
    /// through this so a failure becomes a message instead.
    /// </summary>
    private async Task RunAsync(Func<Task> action)
    {
        if (_viewModel.IsBusy)
            return;

        _viewModel.IsBusy = true;
        _viewModel.Status = "";
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            _viewModel.Status = $"{Loc["Common_Error"]}: {ex.Message}";
        }
        finally
        {
            _viewModel.IsBusy = false;
        }
    }
}
