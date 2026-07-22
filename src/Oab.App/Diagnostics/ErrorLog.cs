using System.Globalization;
using System.Text;

namespace Oab.App.Diagnostics;

/// <summary>
/// A plain-text file of everything that went wrong, newest last. It exists so a
/// crash on a shopkeeper's phone leaves evidence: there is no console to read, no
/// crash reporter, and no network to send one to.
/// <para>
/// The one hard rule is that <b>this class never throws</b>. It is called from
/// catch blocks and from the process-wide handlers of a dying app; a logger that
/// can fail is strictly worse than no logger, because it turns a recoverable
/// error into the crash it was supposed to record.
/// </para>
/// Takes its path as a constructor argument rather than reading
/// <c>FileSystem.AppDataDirectory</c> itself, which is what lets it be tested
/// against a temp directory with no device.
/// </summary>
public sealed class ErrorLog(string filePath)
{
    /// <summary>
    /// The whole file is read into memory to trim it, and it may be attached to a
    /// share sheet, so it stays small. Roughly 40–60 records — far more than
    /// anyone will read, far less than anything a phone would notice.
    /// </summary>
    internal const int MaxBytes = 64 * 1024;

    /// <summary>Every record starts with this at the beginning of a line.</summary>
    internal const string RecordMarker = "=== ";

    /// <summary>
    /// The marker anchored to a line start. Searching for the bare marker would
    /// also match one that happened to appear inside an exception message.
    /// </summary>
    internal const string RecordSeparator = "\n" + RecordMarker;

    internal const string TrimNotice = "(older entries dropped)";

    // Written with a BOM for the same reason the backup summary is (D12): these
    // files get opened in Windows Notepad and Android text viewers, and an
    // exception message can contain Arabic.
    private static readonly UTF8Encoding Utf8 = new(true);

    private readonly Lock _gate = new();

    /// <summary>
    /// Set once by <c>UseOab</c>, in the same shape as
    /// <see cref="Localization.LocalizationManager.Current"/>. Static because the
    /// page extension that logs handler failures is itself static, and because
    /// the process-wide handlers run where there is no DI scope to ask.
    /// </summary>
    public static ErrorLog? Current { get; internal set; }

    public string FilePath { get; } = filePath;

    /// <summary>
    /// Whether anything has been logged. The backup screen hides its "send the
    /// error log" card unless this is true — a button that does nothing on a
    /// healthy phone is a button that erodes trust.
    /// </summary>
    public bool HasEntries
    {
        get
        {
            try
            {
                var file = new FileInfo(FilePath);
                return file.Exists && file.Length > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Appends one record. <paramref name="context"/> is where it happened —
    /// <c>"SuppliersPage.OnRecordPaymentClicked"</c> — which is the part a stack
    /// trace on a release build is least likely to tell you.
    /// </summary>
    public void Write(string context, Exception? error)
    {
        try
        {
            var record = Format(DateTimeOffset.Now, context, error);
            lock (_gate)
            {
                var directory = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                File.AppendAllText(FilePath, record, Utf8);

                var existing = File.ReadAllText(FilePath, Utf8);
                var trimmed = Trim(existing, MaxBytes);
                if (!ReferenceEquals(existing, trimmed))
                    File.WriteAllText(FilePath, trimmed, Utf8);
            }
        }
        catch (Exception)
        {
            // Deliberately swallowed, and the only empty catch in the codebase.
            // There is nowhere left to report a failure to report a failure.
        }
    }

    /// <summary>The whole log, or an empty string if nothing has been written.</summary>
    public string ReadAll()
    {
        try
        {
            lock (_gate)
                return File.Exists(FilePath) ? File.ReadAllText(FilePath, Utf8) : "";
        }
        catch (Exception)
        {
            return "";
        }
    }

    public void Clear()
    {
        try
        {
            lock (_gate)
                File.Delete(FilePath);
        }
        catch (Exception)
        {
            // Same reasoning as Write.
        }
    }

    /// <summary>
    /// One record. Timestamps are formatted with <see cref="CultureInfo.InvariantCulture"/>
    /// on purpose: the app sets the thread culture to <c>ar</c>, which would
    /// otherwise write dates in Arabic-Indic digits and possibly a Hijri
    /// calendar. This file is read by a developer, not a shopkeeper.
    /// <para>
    /// <c>Exception.ToString()</c> carries type, message, stack, and the whole
    /// inner-exception chain, which is everything worth having.
    /// </para>
    /// Newlines are <c>'\n'</c> explicitly so the bytes are identical whichever
    /// platform produced them.
    /// </summary>
    internal static string Format(DateTimeOffset when, string context, Exception? error)
    {
        var text = new StringBuilder();
        text.Append(RecordMarker)
            .Append(when.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture))
            .Append("  ")
            .Append(string.IsNullOrWhiteSpace(context) ? "(no context)" : context)
            .Append('\n')
            .Append(error?.ToString() ?? "(no exception object)")
            .Append('\n')
            .Append('\n');
        return text.ToString();
    }

    /// <summary>
    /// Drops the oldest records once the file outgrows <paramref name="maxBytes"/>,
    /// keeping roughly the newest half. The cut is moved forward to the next
    /// record boundary so the file never begins mid-stack-trace, and the drop is
    /// announced rather than silent — a log that quietly loses its beginning is a
    /// log you can misread.
    /// <para>Returns the argument unchanged when nothing needs dropping, which is
    /// how <see cref="Write"/> avoids rewriting the file on the common path.</para>
    /// </summary>
    internal static string Trim(string log, int maxBytes)
    {
        if (Encoding.UTF8.GetByteCount(log) <= maxBytes)
            return log;

        var boundary = log.IndexOf(RecordSeparator, log.Length / 2, StringComparison.Ordinal);
        var kept = boundary >= 0 ? log[(boundary + 1)..] : "";
        return $"{TrimNotice}\n\n{kept}";
    }
}
