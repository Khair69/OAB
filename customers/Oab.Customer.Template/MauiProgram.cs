using Microsoft.Extensions.Logging;
using Oab.App;
using Oab.Modules.Purchases;
using Oab.Modules.SupplierDebts;

namespace Oab.Customer.Template;

/// <summary>
/// A whole customer build is this file: the shop's config plus the modules
/// they asked for. To onboard a new shop, copy this project, change the
/// ShopConfig, and add/remove modules.
/// </summary>
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseOab(
                new ShopConfig
                {
                    ShopName = "متجر تجريبي", // demo shop
                    CurrencySymbol = "د.ج",
                    DefaultCulture = "ar",
                    SupportedCultures = ["ar", "en"],
                    // Example of per-shop wording without code:
                    // LabelOverrides = new Dictionary<string, string> { ["ar:Purchases_Title"] = "الدفتر" },
                },
                new PurchasesModule(),
                new SupplierDebtsModule())
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
