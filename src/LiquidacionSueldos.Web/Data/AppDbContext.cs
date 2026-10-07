using System.Linq.Expressions;
using LiquidacionSueldos.Web.Domain;
using LiquidacionSueldos.Web.Services;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LiquidacionSueldos.Web.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>, IDataProtectionKeyContext
{
    private readonly ITenantContext? _tenant;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext? tenant = null) : base(options)
    {
        _tenant = tenant;
    }

    /// <summary>Estudio del usuario autenticado. Se usa en los filtros globales (aislamiento entre estudios).</summary>
    public Guid CurrentEstudioId => _tenant?.EstudioId ?? Guid.Empty;

    public DbSet<Estudio> Estudios => Set<Estudio>();
    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<Convenio> Convenios => Set<Convenio>();
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Concepto> Conceptos => Set<Concepto>();
    public DbSet<Empleado> Empleados => Set<Empleado>();
    public DbSet<Liquidacion> Liquidaciones => Set<Liquidacion>();
    public DbSet<LiquidacionLinea> LiquidacionLineas => Set<LiquidacionLinea>();
    public DbSet<Vacacion> Vacaciones => Set<Vacacion>();
    public DbSet<Nota> Notas => Set<Nota>();
    public DbSet<AuditoriaEntry> Auditorias => Set<AuditoriaEntry>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(18, 6);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<ApplicationUser>(e =>
        {
            e.HasIndex(u => u.EstudioId);
        });

        b.Entity<Empresa>(e => e.HasIndex(x => x.EstudioId));

        b.Entity<Convenio>(e =>
        {
            e.HasIndex(x => x.EstudioId);
            e.HasMany(x => x.Categorias).WithOne(c => c.Convenio).HasForeignKey(c => c.ConvenioId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Categoria>(e => e.HasIndex(x => new { x.EstudioId, x.ConvenioId }));
        b.Entity<Concepto>(e => e.HasIndex(x => x.EstudioId));

        b.Entity<Empleado>(e =>
        {
            e.HasIndex(x => new { x.EmpresaId, x.Legajo }).IsUnique();
            e.HasIndex(x => x.EstudioId);
            e.HasOne(x => x.Convenio).WithMany().HasForeignKey(x => x.ConvenioId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Categoria).WithMany().HasForeignKey(x => x.CategoriaId).OnDelete(DeleteBehavior.Restrict);
            e.Ignore(x => x.NombreCompleto);
            e.Ignore(x => x.BasicoEfectivo);
        });

        b.Entity<Liquidacion>(e =>
        {
            e.HasIndex(x => new { x.EmpresaId, x.Periodo, x.Tipo, x.Version }).IsUnique();
            e.HasIndex(x => x.EstudioId);
            e.Ignore(x => x.EstaCerrada);
            e.HasMany(x => x.Lineas).WithOne(l => l.Liquidacion).HasForeignKey(l => l.LiquidacionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<LiquidacionLinea>(e =>
        {
            e.HasIndex(x => new { x.LiquidacionId, x.EmpleadoId }).IsUnique();
            e.HasOne(x => x.Empleado).WithMany().HasForeignKey(x => x.EmpleadoId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.ConceptosJson).HasColumnType("jsonb");
            e.Property(x => x.SnapshotJson).HasColumnType("jsonb");
        });

        b.Entity<Vacacion>(e =>
        {
            e.HasIndex(x => new { x.EmpresaId, x.EmpleadoId });
            e.HasOne(x => x.Empleado).WithMany().HasForeignKey(x => x.EmpleadoId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Nota>(e =>
        {
            e.HasIndex(x => new { x.EmpresaId, x.EmpleadoId });
            e.HasOne(x => x.Empleado).WithMany().HasForeignKey(x => x.EmpleadoId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<AuditoriaEntry>(e =>
        {
            e.HasIndex(x => new { x.EstudioId, x.Fecha });
        });

        // Claves Guid asignadas por la aplicación: EF debe tratar los hijos nuevos como "Added" (no "Modified").
        foreach (var type in b.Model.GetEntityTypes().ToList())
        {
            var pk = type.FindPrimaryKey();
            if (pk is { Properties.Count: 1 } && pk.Properties[0].ClrType == typeof(Guid))
                b.Entity(type.ClrType).Property(pk.Properties[0].Name).ValueGeneratedNever();
        }

        // Aislamiento multi-estudio: toda entidad IEstudioOwned se filtra automáticamente por el estudio actual.
        foreach (var type in b.Model.GetEntityTypes().Where(t => typeof(IEstudioOwned).IsAssignableFrom(t.ClrType)).ToList())
        {
            var p = Expression.Parameter(type.ClrType, "e");
            var body = Expression.Equal(
                Expression.Property(p, nameof(IEstudioOwned.EstudioId)),
                Expression.Property(Expression.Constant(this), nameof(CurrentEstudioId)));
            b.Entity(type.ClrType).HasQueryFilter(Expression.Lambda(body, p));
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EstamparEstudio();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        EstamparEstudio();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>Asigna el estudio actual a las entidades nuevas que no lo traen y evita cambios de estudio.</summary>
    private void EstamparEstudio()
    {
        var actual = CurrentEstudioId;
        foreach (var entry in ChangeTracker.Entries<IEstudioOwned>())
        {
            if (entry.State == EntityState.Added && entry.Entity.EstudioId == Guid.Empty && actual != Guid.Empty)
                entry.Entity.EstudioId = actual;
            else if (entry.State == EntityState.Modified)
                entry.Property(nameof(IEstudioOwned.EstudioId)).IsModified = false;
        }
    }
}
