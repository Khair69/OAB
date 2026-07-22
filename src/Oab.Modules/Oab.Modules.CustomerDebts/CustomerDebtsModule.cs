using Microsoft.Extensions.DependencyInjection;
using Oab.App.Modules;

namespace Oab.Modules.CustomerDebts;

public class CustomerDebtsModule : IOabModule
{
    public string Name => "CustomerDebts";

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<CustomersViewModel>();
    }

    public IEnumerable<OabNavItem> GetNavItems()
    {
        yield return new OabNavItem("Customers_Title", "customers", typeof(CustomersPage));
    }
}
