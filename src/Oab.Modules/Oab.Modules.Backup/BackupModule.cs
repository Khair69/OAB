using Microsoft.Extensions.DependencyInjection;
using Oab.App.Modules;

namespace Oab.Modules.Backup;

public class BackupModule : IOabModule
{
    public string Name => "Backup";

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<BackupViewModel>();
    }

    public IEnumerable<OabNavItem> GetNavItems()
    {
        yield return new OabNavItem("Backup_Title", "backup", typeof(BackupPage));
    }
}
