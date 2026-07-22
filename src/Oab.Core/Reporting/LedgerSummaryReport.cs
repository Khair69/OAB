using System.Globalization;
using System.Text;
using Oab.Core.Formatting;

namespace Oab.Core.Reporting;

/// <summary>One party and where they stand, for the readable summary.</summary>
public sealed record SummaryLine(string PartyName, decimal Balance);

/// <summary>
/// Wording for the summary. Passed in rather than looked up so this stays a
/// pure, testable function — the app layer supplies localized text.
/// </summary>
public sealed record SummaryLabels(string YouOwe, string OwesYou, string Settled, string Total);

/// <summary>
/// Renders the whole book as plain text a shopkeeper can read in WhatsApp —
/// the human-readable half of a backup. If the app or the phone is ever lost,
/// this is the copy a person can still act on without any software.
/// </summary>
public static class LedgerSummaryReport
{
    public static string Build(
        string shopName,
        DateTimeOffset generatedAt,
        IEnumerable<SummaryLine> lines,
        SummaryLabels labels,
        CultureInfo culture,
        string currencySymbol = "",
        bool useArabicIndicDigits = false)
    {
        var all = lines.ToList();
        // Sign convention (see LedgerEntry): negative = the shop owes them.
        var shopOwes = all.Where(l => l.Balance < 0m).OrderBy(l => l.Balance).ToList();
        var owedToShop = all.Where(l => l.Balance > 0m).OrderByDescending(l => l.Balance).ToList();
        var settled = all.Count(l => l.Balance == 0m);

        var text = new StringBuilder();
        // "\n" rather than AppendLine so output is identical on every platform.
        text.Append(shopName).Append('\n');
        text.Append(generatedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)).Append('\n');

        AppendSection(text, labels.YouOwe, shopOwes);
        AppendSection(text, labels.OwesYou, owedToShop);

        // A count, not an amount — don't run it through the money formatter.
        text.Append('\n').Append(labels.Settled).Append(": ").Append(settled.ToString(culture)).Append('\n');
        return text.ToString();

        void AppendSection(StringBuilder sb, string heading, List<SummaryLine> section)
        {
            if (section.Count == 0)
                return;

            sb.Append('\n').Append("== ").Append(heading).Append(" ==").Append('\n');
            foreach (var line in section)
                sb.Append("- ").Append(line.PartyName).Append(": ").Append(Money(line.Balance)).Append('\n');
            sb.Append(labels.Total).Append(": ").Append(Money(section.Sum(l => l.Balance))).Append('\n');
        }

        string Money(decimal amount) =>
            MoneyFormat.Format(amount, culture, currencySymbol, useArabicIndicDigits);
    }
}
