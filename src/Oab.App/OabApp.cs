using Microsoft.EntityFrameworkCore;
using Oab.Data;

namespace Oab.App;

public class OabApp : Application
{
    private readonly IServiceProvider _services;
    private readonly ShopConfig _config;

    public OabApp(IServiceProvider services, ShopConfig config, IDbContextFactory<OabDbContext> dbFactory)
    {
        _services = services;
        _config = config;
        OabServices.Provider = services;

        // Bring the shop's local database up to the current schema before any
        // page can touch it. Local SQLite, so this is fast even on old phones.
        using var db = dbFactory.CreateDbContext();
        db.Database.Migrate();
    }

    protected override Window CreateWindow(IActivationState? activationState) =>
        new(_services.GetRequiredService<OabShell>()) { Title = _config.ShopName };
}
