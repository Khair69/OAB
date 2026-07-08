using Microsoft.Extensions.DependencyInjection;
using Oab.App.Modules;

namespace Oab.Modules.Purchases;

public class PurchasesModule : IOabModule
{
    public string Name => "Purchases";

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<PurchasesListViewModel>();
        services.AddTransient<NewPurchaseViewModel>();
        services.AddTransient<NewPurchasePage>();
    }

    public IEnumerable<OabNavItem> GetNavItems()
    {
        yield return new OabNavItem("Purchases_Title", "purchases", typeof(PurchasesListPage));
    }
}
