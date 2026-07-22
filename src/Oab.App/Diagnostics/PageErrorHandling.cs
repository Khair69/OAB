using System.Runtime.CompilerServices;
using Oab.App.Localization;

namespace Oab.App.Diagnostics;

/// <summary>
/// The one place a page turns a failure into a message instead of a crash.
/// <para>
/// MAUI event handlers must be <c>async void</c>: there is no <c>Task</c> for
/// anyone to await, so an escaping exception goes straight to the process-wide
/// handler and the app disappears mid-tap with no explanation. Two pages had
/// grown a private copy of this method, which is what said it belonged here.
/// </para>
/// An extension method rather than a base page class, because a base class would
/// have to be inherited by every page in every module — and modules are meant to
/// be able to hold a plain <c>ContentPage</c>.
/// </summary>
public static class PageErrorHandling
{
    /// <summary>
    /// Runs <paramref name="action"/>, and on failure logs it and tells the user.
    /// <para>
    /// <paramref name="onError"/> lets a page report in its own idiom — the
    /// backup screen writes to its status label rather than throwing a dialog on
    /// top of a share sheet. When it is null the failure becomes an alert.
    /// </para>
    /// <paramref name="context"/> defaults to the calling handler's name, so
    /// every log record says which button was pressed without a single call site
    /// having to spell it out.
    /// </summary>
    public static async Task RunSafelyAsync(
        this Page page,
        Func<Task> action,
        Action<Exception>? onError = null,
        [CallerMemberName] string context = "")
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            ErrorLog.Current?.Write($"{page.GetType().Name}.{context}", ex);

            try
            {
                if (onError is not null)
                {
                    onError(ex);
                }
                else
                {
                    // The title is the sentence a shopkeeper can act on; the body
                    // is the developer's half, and it is there because a
                    // screenshot sent over WhatsApp is how support actually
                    // happens for this product.
                    var loc = LocalizationManager.Current;
                    await page.DisplayAlertAsync(
                        loc?["Common_Error"] ?? "Something went wrong",
                        ex.Message,
                        loc?["Common_OK"] ?? "OK");
                }
            }
            catch (Exception reportingFailure)
            {
                // Reporting can fail too — a dialog during teardown, a page
                // already popped. Record it and stop; re-throwing here would
                // crash the app over the error message rather than the error.
                ErrorLog.Current?.Write($"{page.GetType().Name}.{context} (while reporting)", reportingFailure);
            }
        }
    }
}
