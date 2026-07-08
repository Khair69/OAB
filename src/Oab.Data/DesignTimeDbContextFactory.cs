using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Oab.Data;

/// <summary>Used only by `dotnet ef migrations add` — never at runtime.</summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<OabDbContext>
{
    public OabDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<OabDbContext>()
            .UseSqlite("Data Source=design-time.db")
            .Options;
        return new OabDbContext(options);
    }
}
