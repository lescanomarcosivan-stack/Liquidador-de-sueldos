using LiquidacionSueldos.Web.Data;
using LiquidacionSueldos.Web.Domain;
using Microsoft.EntityFrameworkCore;

namespace LiquidacionSueldos.Web.Services;

public record UsoPlan(PlanInfo Plan, int Empresas, int Empleados, int Usuarios);

/// <summary>Consulta el plan y la prueba gratuita del estudio actual y controla que no se superen sus límites.</summary>
public class PlanService(AppDbContext db, ITenantContext tenant)
{
    // El servicio vive una sola solicitud: se lee el estudio una vez y se reutiliza (menú, filtro, página).
    private Estudio? _estudio;
    private bool _cargado;

    private async Task<Estudio?> EstudioAsync()
    {
        if (!_cargado)
        {
            _estudio = await db.Estudios.AsNoTracking().FirstOrDefaultAsync(e => e.Id == tenant.EstudioId);
            _cargado = true;
        }
        return _estudio;
    }

    public async Task<PlanInfo> PlanActualAsync() => Planes.Obtener((await EstudioAsync())?.Plan);

    /// <summary>Estado de la prueba gratuita (NoAplica si el estudio tiene otro plan o no hay sesión).</summary>
    public async Task<EstadoPrueba> PruebaAsync()
    {
        var e = await EstudioAsync();
        return e == null ? EstadoPrueba.NoAplica : Planes.CalcularPrueba(e.Plan, e.CreadoEn, e.PruebaVence, DateTime.UtcNow);
    }

    public async Task<UsoPlan> UsoAsync()
    {
        var plan = await PlanActualAsync();
        var estudioId = tenant.EstudioId;
        return new UsoPlan(
            plan,
            await db.Empresas.CountAsync(),
            await db.Empleados.CountAsync(e => e.Activo),
            await db.Users.CountAsync(u => u.EstudioId == estudioId && u.Activo));
    }

    /// <summary>Devuelve el mensaje de error si agregar un recurso más no corresponde (prueba vencida o tope del plan); null si se puede.</summary>
    public async Task<string?> ValidarAltaAsync(Recurso recurso)
    {
        if ((await PruebaAsync()).Vencida) return Planes.MensajePruebaVencida;

        var plan = await PlanActualAsync();
        if (plan.Max(recurso) == int.MaxValue) return null;
        var estudioId = tenant.EstudioId;
        var actual = recurso switch
        {
            Recurso.Empresas => await db.Empresas.CountAsync(),
            Recurso.Empleados => await db.Empleados.CountAsync(e => e.Activo),
            _ => await db.Users.CountAsync(u => u.EstudioId == estudioId && u.Activo)
        };
        return Planes.AlcanzoElLimite(plan, recurso, actual) ? Planes.MensajeLimite(plan, recurso) : null;
    }
}
