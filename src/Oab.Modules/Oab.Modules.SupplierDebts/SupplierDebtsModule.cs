using Microsoft.Extensions.DependencyInjection;
using Oab.App.Modules;

namespace Oab.Modules.SupplierDebts;

public class SupplierDebtsModule : IOabModule
{
    public string Name => "SupplierDebts";

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<SuppliersViewModel>();
    }

    public IEnumerable<OabNavItem> GetNavItems()
    {
        yield return new OabNavItem("Suppliers_Title", "suppliers", typeof(SuppliersPage));
    }
}
