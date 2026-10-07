using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LiquidacionSueldos.Web.Data;

/// <summary>Solo para `dotnet ef migrations add`: evita arrancar la aplicación completa.</summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=liquidacion;Username=postgres;Password=postgres")
            .Options;
        return new AppDbContext(opts);
    }
}
