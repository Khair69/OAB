using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Oab.Core.Ledger;

namespace Oab.Data;

public static class OabDataServiceCollectionExtensions
{
    /// <summary>
    /// Registers the SQLite-backed money engine: context factory, store, and
    /// LedgerService. <paramref name="databasePath"/> is the full path to the
    /// shop's .db file (per-device, offline).
    /// </summary>
    public static IServiceCollection AddOabData(this IServiceCollection services, string databasePath)
    {
        services.AddDbContextFactory<OabDbContext>(options =>
            options.UseSqlite($"Data Source={databasePath}"));
        services.AddSingleton<ILedgerStore, LedgerStore>();
        services.AddSingleton<LedgerService>();
        return services;
    }
}
