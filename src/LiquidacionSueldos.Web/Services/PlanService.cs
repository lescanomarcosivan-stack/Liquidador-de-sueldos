using LiquidacionSueldos.Web.Data;
using LiquidacionSueldos.Web.Domain;
using Microsoft.EntityFrameworkCore;

namespace LiquidacionSueldos.Web.Services;

public record UsoPlan(PlanInfo Plan, int Empresas, int Empleados, int Usuarios);

/// <summary>Consulta el plan del estudio actual y controla que no se superen sus límites.</summary>
public class PlanService(AppDbContext db, ITenantContext tenant)
{
    public async Task<PlanInfo> PlanActualAsync()
    {
        var codigo = await db.Estudios.Where(e => e.Id == tenant.EstudioId).Select(e => e.Plan).FirstOrDefaultAsync();
        return Planes.Obtener(codigo);
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

    /// <summary>Devuelve el mensaje de error si agregar un recurso más supera el plan; null si se puede.</summary>
    public async Task<string?> ValidarAltaAsync(Recurso recurso)
    {
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
