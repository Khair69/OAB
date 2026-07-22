namespace Oab.App.Diagnostics;

/// <summary>
/// The last line of defence. <see cref="PageErrorHandling.RunSafelyAsync"/> catches
/// what a page can see coming; this catches everything else — a background task
/// nobody awaited, an exception on a thread that has no page, and the startup
/// path itself, including the <c>Database.Migrate()</c> call in the
/// <see cref="OabApp"/> constructor (D10), whose failure is otherwise an
/// instant, silent, unexplainable crash.
/// <para>
/// <b>It records; it does not recover.</b> None of these handlers mark the
/// exception handled or keep the process alive. By the time they run the app's
/// state is unknown, and a ledger app that carries on in an unknown state can
/// write a wrong number — which is worse than closing. The goal is that the next
/// launch can explain what happened.
/// </para>
/// </summary>
public static class GlobalExceptionHandler
{
    private static bool _installed;

    /// <summary>
    /// Idempotent, because a second <c>UseOab</c> in a test host would otherwise
    /// double every record.
    /// </summary>
    public static void Install(ErrorLog log)
    {
        if (_installed)
            return;
        _installed = true;

        // Fires for an unhandled exception on any managed thread. On most
        // platforms the process is already going down when this runs, so the
        // write has to be synchronous — which it is.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            log.Write("AppDomain.UnhandledException", e.ExceptionObject as Exception);

        // A faulted Task nobody awaited. This is the quiet one: it surfaces at
        // some arbitrary later garbage collection, and without SetObserved the
        // default is to say nothing at all. Every `async void` handler in the app
        // can produce one.
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            log.Write("TaskScheduler.UnobservedTaskException", e.Exception);
            e.SetObserved();
        };

#if ANDROID
        // The real product. Exceptions crossing the Java/managed boundary — which
        // is most of what a MAUI event handler runs inside — arrive here and not
        // always at AppDomain.UnhandledException.
        Android.Runtime.AndroidEnvironment.UnhandledExceptionRaiser += (_, e) =>
            log.Write("AndroidEnvironment.UnhandledExceptionRaiser", e.Exception);
#endif

#if WINDOWS
        // Development only, but this is where every bug is first seen. Current is
        // null-checked because UseOab runs inside CreateMauiApp and the ordering
        // is not ours to promise.
        var winui = Microsoft.UI.Xaml.Application.Current;
        if (winui is not null)
        {
            winui.UnhandledException += (_, e) =>
                log.Write("WinUI.Application.UnhandledException", e.Exception);
        }
#endif
    }
}
