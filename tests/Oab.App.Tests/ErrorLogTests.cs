using System.Globalization;
using System.Text;
using Oab.App.Diagnostics;

namespace Oab.App.Tests;

/// <summary>
/// Against real files in a real temp directory, deliberately — the whole value
/// of this class is what it does when the file system misbehaves, and a fake
/// file system would only prove the fake works.
/// </summary>
public class ErrorLogTests : IDisposable
{
    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"oab-errorlog-{Guid.NewGuid():N}.log");

    public void Dispose()
    {
        if (File.Exists(_path))
            File.Delete(_path);
        GC.SuppressFinalize(this);
    }

    private ErrorLog NewLog() => new(_path);

    [Fact]
    public void ARecordCarriesTheContext_TheType_TheMessage_AndTheStack()
    {
        var log = NewLog();

        try
        {
            throw new InvalidOperationException("supplier vanished");
        }
        catch (Exception ex)
        {
            log.Write("SuppliersPage.OnRecordPaymentClicked", ex);
        }

        var text = log.ReadAll();
        Assert.Contains("SuppliersPage.OnRecordPaymentClicked", text, StringComparison.Ordinal);
        Assert.Contains("System.InvalidOperationException", text, StringComparison.Ordinal);
        Assert.Contains("supplier vanished", text, StringComparison.Ordinal);
        // A thrown exception has a stack; the frame naming this test proves it survived.
        Assert.Contains(nameof(ARecordCarriesTheContext_TheType_TheMessage_AndTheStack), text, StringComparison.Ordinal);
    }

    [Fact]
    public void TheInnerExceptionChainSurvives()
    {
        var log = NewLog();
        var inner = new IOException("disk is full");

        log.Write("BackupPage.OnShareDatabaseClicked", new InvalidOperationException("snapshot failed", inner));

        var text = log.ReadAll();
        Assert.Contains("snapshot failed", text, StringComparison.Ordinal);
        Assert.Contains("disk is full", text, StringComparison.Ordinal);
    }

    [Fact]
    public void WritesAppend_NewestLast()
    {
        var log = NewLog();

        log.Write("first", new InvalidOperationException("one"));
        log.Write("second", new InvalidOperationException("two"));

        var text = log.ReadAll();
        Assert.True(text.IndexOf("one", StringComparison.Ordinal) < text.IndexOf("two", StringComparison.Ordinal));
    }

    [Fact]
    public void HasEntries_IsFalseUntilSomethingIsWritten()
    {
        var log = NewLog();
        Assert.False(log.HasEntries);

        log.Write("somewhere", new InvalidOperationException("boom"));

        Assert.True(log.HasEntries);
    }

    [Fact]
    public void ReadingALogThatDoesNotExistIsAnEmptyString_NotAnException()
    {
        Assert.Equal("", NewLog().ReadAll());
    }

    [Fact]
    public void Clear_RemovesEverything()
    {
        var log = NewLog();
        log.Write("somewhere", new InvalidOperationException("boom"));

        log.Clear();

        Assert.False(log.HasEntries);
        Assert.Equal("", log.ReadAll());
    }

    /// <summary>
    /// The hard rule. Every caller is a catch block, and several of them are on
    /// a dying process — a logger that can throw turns a handled error into the
    /// crash it was written to prevent.
    /// </summary>
    [Fact]
    public void WritingToAnImpossiblePathDoesNotThrow()
    {
        // A directory where a file should be: every operation fails, none loudly.
        var log = new ErrorLog(Path.GetTempPath());

        var thrown = Record.Exception(() => log.Write("anywhere", new InvalidOperationException("boom")));

        Assert.Null(thrown);
        Assert.False(log.HasEntries);
        Assert.Equal("", log.ReadAll());
    }

    [Fact]
    public void ANullExceptionIsRecordedRatherThanDropped()
    {
        var log = NewLog();

        log.Write("AppDomain.UnhandledException", null);

        Assert.Contains("(no exception object)", log.ReadAll(), StringComparison.Ordinal);
    }

    /// <summary>
    /// <c>AppDomain.UnhandledException</c> hands over an <c>object</c>, which is
    /// not guaranteed to be an <c>Exception</c> — the cast in
    /// <c>GlobalExceptionHandler</c> can therefore produce null, and the record
    /// still has to say when and where.
    /// </summary>
    [Fact]
    public void ARecordWithNoExceptionStillCarriesItsContext()
    {
        var log = NewLog();

        log.Write("AppDomain.UnhandledException", null);

        Assert.Contains("AppDomain.UnhandledException", log.ReadAll(), StringComparison.Ordinal);
    }

    [Fact]
    public void AMissingContextIsLabelled_NotBlank()
    {
        Assert.Contains("(no context)", ErrorLog.Format(DateTimeOffset.Now, "   ", null), StringComparison.Ordinal);
    }

    /// <summary>
    /// The app sets the thread culture to Arabic. A log timestamped
    /// <c>١٤٤٧/٠١/٢٦</c> in the Umm al-Qura calendar is not something anyone can
    /// line up against a bug report, so the timestamp is invariant on purpose.
    /// </summary>
    [Fact]
    public void TimestampsAreInvariant_EvenWhenTheAppIsRunningInArabic()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            var when = new DateTimeOffset(2026, 7, 22, 14, 3, 11, TimeSpan.FromHours(3));

            var record = ErrorLog.Format(when, "somewhere", null);

            Assert.Contains("2026-07-22 14:03:11 +03:00", record, StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void EveryRecordStartsWithTheMarkerOnItsOwnLine()
    {
        var log = NewLog();
        log.Write("first", new InvalidOperationException("one"));
        log.Write("second", new InvalidOperationException("two"));

        var markers = log.ReadAll()
            .Split('\n')
            .Count(line => line.StartsWith(ErrorLog.RecordMarker, StringComparison.Ordinal));

        Assert.Equal(2, markers);
    }

    [Fact]
    public void UnderTheSizeLimit_TrimChangesNothing()
    {
        var log = ErrorLog.Format(DateTimeOffset.Now, "somewhere", new InvalidOperationException("boom"));

        Assert.Same(log, ErrorLog.Trim(log, ErrorLog.MaxBytes));
    }

    [Fact]
    public void OverTheSizeLimit_TheOldestRecordsGo_AndTheNewestStay()
    {
        var records = Enumerable.Range(0, 200)
            .Select(i => ErrorLog.Format(DateTimeOffset.Now, $"handler-{i}", new InvalidOperationException($"failure-{i}")));
        var log = string.Concat(records);

        var trimmed = ErrorLog.Trim(log, 4096);

        Assert.DoesNotContain("failure-0\n", trimmed, StringComparison.Ordinal);
        Assert.Contains("failure-199", trimmed, StringComparison.Ordinal);
        Assert.StartsWith(ErrorLog.TrimNotice, trimmed, StringComparison.Ordinal);
    }

    /// <summary>
    /// Cutting at an arbitrary byte offset would leave the file starting halfway
    /// through a stack trace, which reads as a different exception than the one
    /// that happened.
    /// </summary>
    [Fact]
    public void TrimmingCutsOnARecordBoundary()
    {
        var records = Enumerable.Range(0, 200)
            .Select(i => ErrorLog.Format(DateTimeOffset.Now, $"handler-{i}", new InvalidOperationException($"failure-{i}")));

        var trimmed = ErrorLog.Trim(string.Concat(records), 4096);

        // Everything after the dropped-entries notice is whole records.
        var body = trimmed[(trimmed.IndexOf(ErrorLog.RecordSeparator, StringComparison.Ordinal) + 1)..];
        Assert.StartsWith(ErrorLog.RecordMarker, body, StringComparison.Ordinal);
    }

    [Fact]
    public void AFileThatOutgrowsTheLimitIsTrimmedOnTheNextWrite()
    {
        var log = NewLog();
        // Each record is a few hundred bytes; 64 KB is a few hundred records.
        for (var i = 0; i < 600; i++)
            log.Write($"handler-{i}", new InvalidOperationException(new string('x', 400)));

        Assert.True(new FileInfo(_path).Length <= ErrorLog.MaxBytes * 2);
        Assert.Contains("handler-599", log.ReadAll(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Arabic in an exception message has to come back out as Arabic — the file
    /// is written with a BOM for exactly this, the same as the backup summary.
    /// </summary>
    [Fact]
    public void ArabicSurvivesTheRoundTrip()
    {
        var log = NewLog();

        log.Write("مكان ما", new InvalidOperationException("لا بدّ من ذكر سبب التصحيح"));

        Assert.Contains("لا بدّ من ذكر سبب التصحيح", log.ReadAll(), StringComparison.Ordinal);
        Assert.Equal(Encoding.UTF8.GetPreamble(), File.ReadAllBytes(_path)[..3]);
    }
}
