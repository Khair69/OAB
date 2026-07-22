using Oab.App.Diagnostics;
using Oab.App.Formatting;
using Oab.App.Localization;
using Oab.App.Modules;
using Oab.App.Views;
using Oab.Data;

namespace Oab.App;

public static class OabAppBuilderExtensions
{
    /// <summary>
    /// The single entry point a customer head project calls:
    ///   builder.UseOab(shopConfig, new PurchasesModule(), new SupplierDebtsModule(), ...);
    /// Order of modules = order in the flyout menu.
    /// </summary>
    public static MauiAppBuilder UseOab(this MauiAppBuilder builder, ShopConfig config, params IOabModule[] modules)
    {
        // Before anything else, including UseMauiApp: from this line on, a crash
        // anywhere — module registration, the OabApp constructor's Migrate() call
        // (D10), the first page load — leaves a record instead of nothing.
        var errorLog = new ErrorLog(Path.Combine(FileSystem.AppDataDirectory, "errors.log"));
        ErrorLog.Current = errorLog;
        GlobalExceptionHandler.Install(errorLog);

        builder.UseMauiApp<OabApp>();

        var services = builder.Services;
        services.AddSingleton(errorLog);
        services.AddSingleton(config);
        services.AddSingleton(_ =>
        {
            var localization = new LocalizationManager(config, Preferences.Default);
            LocalizationManager.Current = localization;
            return localization;
        });
        services.AddSingleton<IMoneyFormatter, MoneyFormatter>();
        services.AddOabData(Path.Combine(FileSystem.AppDataDirectory, config.DatabaseFileName));
        services.AddSingleton<IReadOnlyList<IOabModule>>(modules);
        services.AddSingleton<OabShell>();

        // Shared detail pages every shop gets, module list notwithstanding.
        services.AddTransient<PartyStatementViewModel>();
        services.AddTransient<PartyStatementPage>();

        foreach (var module in modules)
        {
            module.ConfigureServices(services);
            module.RegisterRoutes();
            foreach (var nav in module.GetNavItems())
                services.AddTransient(nav.PageType);
        }

        return builder;
    }
}
