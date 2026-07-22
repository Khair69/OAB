using Oab.App.Formatting;
using Oab.App.Localization;
using Oab.App.Modules;
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
        builder.UseMauiApp<OabApp>();

        var services = builder.Services;
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
