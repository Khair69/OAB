using System.Globalization;
using Oab.Core.Reporting;

namespace Oab.Core.Tests;

public class LedgerSummaryReportTests
{
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");
    private static readonly DateTimeOffset At = new(2026, 7, 22, 14, 30, 0, TimeSpan.Zero);
    private static readonly SummaryLabels Labels = new("You owe", "Owed to you", "Settled", "Total");

    private static string Build(params SummaryLine[] lines) =>
        LedgerSummaryReport.Build("Test Shop", At, lines, Labels, En, "SP");

    [Fact]
    public void GroupsBySide_WithMagnitudesAndTotals()
    {
        var text = Build(
            new SummaryLine("Acme", -500m),      // shop owes them
            new SummaryLine("Beta", -120m),
            new SummaryLine("Sami", 80m),        // they owe the shop
            new SummaryLine("Cleared", 0m));

        Assert.Contains("Test Shop", text, StringComparison.Ordinal);
        Assert.Contains("2026-07-22 14:30", text, StringComparison.Ordinal);
        Assert.Contains("== You owe ==", text, StringComparison.Ordinal);
        Assert.Contains("- Acme: 500.00 SP", text, StringComparison.Ordinal);
        Assert.Contains("- Beta: 120.00 SP", text, StringComparison.Ordinal);
        Assert.Contains("== Owed to you ==", text, StringComparison.Ordinal);
        Assert.Contains("- Sami: 80.00 SP", text, StringComparison.Ordinal);
        Assert.Contains("Total: 620.00 SP", text, StringComparison.Ordinal);
        Assert.Contains("Settled: 1", text, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptySideIsOmitted()
    {
        var text = Build(new SummaryLine("Sami", 80m));

        Assert.DoesNotContain("You owe", text, StringComparison.Ordinal);
        Assert.Contains("Owed to you", text, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyBook_StillProducesAReadableHeader()
    {
        var text = Build();

        Assert.Contains("Test Shop", text, StringComparison.Ordinal);
        Assert.Contains("Settled: 0", text, StringComparison.Ordinal);
    }

    [Fact]
    public void LineEndingsAreStableAcrossPlatforms() =>
        Assert.DoesNotContain('\r', Build(new SummaryLine("Sami", 80m)));

    [Fact]
    public void ArabicIndicDigits_AreHonoured()
    {
        var text = LedgerSummaryReport.Build(
            "متجر", At, [new SummaryLine("سامي", 80m)], Labels, En, "د.ج", useArabicIndicDigits: true);

        Assert.Contains("٨٠.٠٠ د.ج", text, StringComparison.Ordinal);
    }
}
