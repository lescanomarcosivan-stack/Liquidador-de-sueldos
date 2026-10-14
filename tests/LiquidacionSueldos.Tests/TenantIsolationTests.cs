using LiquidacionSueldos.Web.Data;
using LiquidacionSueldos.Web.Domain;
using LiquidacionSueldos.Web.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LiquidacionSueldos.Tests;

/// <summary>
/// Garantías de aislamiento entre estudios contables: lo que carga un estudio nunca lo ve, lee ni modifica otro.
/// Si alguna de estas pruebas falla, hay una fuga de datos entre clientes.
/// </summary>
public class TenantIsolationTests
{
    private sealed class FakeTenant(Guid estudioId) : ITenantContext
    {
        public bool IsAuthenticated => estudioId != Guid.Empty;
        public Guid EstudioId => estudioId;
        public Guid? EmpresaId => null;
        public Guid RequireEmpresaId => throw new InvalidOperationException();
        public string UserId => "test";
        public string DisplayName => "test";
        public bool IsAdmin => true;
        public void SetEmpresa(Guid? empresaId) { }
    }

    private static readonly Guid EstudioA = Guid.NewGuid();
    private static readonly Guid EstudioB = Guid.NewGuid();

    private static DbContextOptions<AppDbContext> NuevaBase() =>
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase("aislamiento-" + Guid.NewGuid()).Options;

    private static AppDbContext Ctx(DbContextOptions<AppDbContext> o, Guid estudio) => new(o, new FakeTenant(estudio));

    /// <summary>Carga una empresa con un empleado en cada estudio y devuelve los ids de la empresa/empleado de A.</summary>
    private static async Task<(Guid empresaA, Guid empleadoA)> Sembrar(DbContextOptions<AppDbContext> o)
    {
        Guid empresaA, empleadoA;
        await using (var a = Ctx(o, EstudioA))
        {
            var e = new Empresa { RazonSocial = "Empresa de A" };
            a.Empresas.Add(e);
            var emp = new Empleado { EmpresaId = e.Id, Legajo = "1", Apellido = "Perez", Nombre = "Ana" };
            a.Empleados.Add(emp);
            await a.SaveChangesAsync();
            empresaA = e.Id; empleadoA = emp.Id;
        }
        await using (var b = Ctx(o, EstudioB))
        {
            var e = new Empresa { RazonSocial = "Empresa de B" };
            b.Empresas.Add(e);
            b.Empleados.Add(new Empleado { EmpresaId = e.Id, Legajo = "1", Apellido = "Gomez", Nombre = "Beto" });
            await b.SaveChangesAsync();
        }
        return (empresaA, empleadoA);
    }

    [Fact]
    public async Task CadaEstudioVeSoloSusPropiosDatos()
    {
        var o = NuevaBase();
        await Sembrar(o);

        await using var a = Ctx(o, EstudioA);
        Assert.Equal(new[] { "Empresa de A" }, await a.Empresas.Select(e => e.RazonSocial).ToArrayAsync());
        Assert.Equal(new[] { "Perez" }, await a.Empleados.Select(e => e.Apellido).ToArrayAsync());

        await using var b = Ctx(o, EstudioB);
        Assert.Equal(new[] { "Empresa de B" }, await b.Empresas.Select(e => e.RazonSocial).ToArrayAsync());
        Assert.Equal(new[] { "Gomez" }, await b.Empleados.Select(e => e.Apellido).ToArrayAsync());
    }

    [Fact]
    public async Task UnEstudioNoPuedeLeerUnRegistroAjenoPorId()
    {
        var o = NuevaBase();
        var (empresaA, empleadoA) = await Sembrar(o);

        await using var b = Ctx(o, EstudioB);
        Assert.Null(await b.Empresas.FirstOrDefaultAsync(e => e.Id == empresaA));
        Assert.Null(await b.Empleados.FirstOrDefaultAsync(e => e.Id == empleadoA));
        Assert.False(await b.Empleados.AnyAsync(e => e.EmpresaId == empresaA));
    }

    [Fact]
    public async Task UnaSesionSinEstudioNoVeNada()
    {
        var o = NuevaBase();
        await Sembrar(o);

        await using var anonimo = Ctx(o, Guid.Empty);
        Assert.Empty(await anonimo.Empresas.ToListAsync());
        Assert.Empty(await anonimo.Empleados.ToListAsync());
    }

    [Fact]
    public async Task LasEntidadesNuevasQuedanEstampadasConElEstudioActual()
    {
        var o = NuevaBase();
        await Sembrar(o);

        await using var cualquiera = Ctx(o, Guid.Empty);
        var todas = await cualquiera.Empresas.IgnoreQueryFilters().ToListAsync();
        Assert.Equal(EstudioA, todas.Single(e => e.RazonSocial == "Empresa de A").EstudioId);
        Assert.Equal(EstudioB, todas.Single(e => e.RazonSocial == "Empresa de B").EstudioId);
    }

    [Fact]
    public async Task NoSePuedeMoverUnRegistroAOtroEstudioModificandoSuEstudioId()
    {
        var o = NuevaBase();
        var (empresaA, _) = await Sembrar(o);

        await using (var a = Ctx(o, EstudioA))
        {
            var e = await a.Empresas.SingleAsync(x => x.Id == empresaA);
            e.EstudioId = EstudioB;           // intento de "regalar" o robar el registro
            e.RazonSocial = "Renombrada";
            await a.SaveChangesAsync();
        }

        await using var verificador = Ctx(o, Guid.Empty);
        var guardada = await verificador.Empresas.IgnoreQueryFilters().SingleAsync(x => x.Id == empresaA);
        Assert.Equal(EstudioA, guardada.EstudioId);   // el estudio no cambió
        Assert.Equal("Renombrada", guardada.RazonSocial); // el resto sí se guardó
    }
}
