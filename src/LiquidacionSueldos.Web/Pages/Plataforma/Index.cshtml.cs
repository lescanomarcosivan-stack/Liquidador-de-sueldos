using LiquidacionSueldos.Web.Data;
using LiquidacionSueldos.Web.Domain;
using LiquidacionSueldos.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LiquidacionSueldos.Web.Pages.Plataforma;

public class FilaEstudio
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = "";
    public DateTime CreadoEn { get; set; }
    public string Plan { get; set; } = "";
    public int Empresas { get; set; }
    public int Empleados { get; set; }
    public int Usuarios { get; set; }
    public EstadoPrueba Prueba { get; set; } = EstadoPrueba.NoAplica;
}

/// <summary>Administración de planes de todos los estudios. Solo para el dueño de la plataforma (SUPERADMIN_EMAIL).</summary>
public class IndexModel(AppDbContext db, IConfiguration config, ITenantContext tenant) : PageModel
{
    public List<FilaEstudio> Estudios { get; set; } = new();
    [TempData] public string? Ok { get; set; }
    [TempData] public string? Error { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!SuperAdmin.EsSuperAdmin(User, config)) return NotFound();
        await CargarAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostPlanAsync(Guid id, string plan)
    {
        if (!SuperAdmin.EsSuperAdmin(User, config)) return NotFound();
        if (!Planes.Existe(plan)) { Error = "Plan inexistente."; return RedirectToPage(); }

        var estudio = await db.Estudios.FirstOrDefaultAsync(e => e.Id == id);
        if (estudio == null) return NotFound();

        var anterior = estudio.Plan;
        estudio.Plan = Planes.Obtener(plan).Codigo;
        // Pasar a Prueba arranca 30 días nuevos; cualquier otro plan no tiene vencimiento.
        estudio.PruebaVence = estudio.Plan == Planes.Prueba ? DateTime.UtcNow.AddDays(Planes.DiasPrueba) : null;
        db.Auditorias.Add(new AuditoriaEntry
        {
            EstudioId = estudio.Id, Usuario = tenant.DisplayName, Accion = "plan_cambiado",
            Detalle = $"Plan del estudio «{estudio.Nombre}»: {anterior} → {estudio.Plan}"
        });
        await db.SaveChangesAsync();
        Ok = $"«{estudio.Nombre}» ahora tiene el plan {Planes.Obtener(estudio.Plan).Nombre}.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostExtenderAsync(Guid id)
    {
        if (!SuperAdmin.EsSuperAdmin(User, config)) return NotFound();
        var estudio = await db.Estudios.FirstOrDefaultAsync(e => e.Id == id);
        if (estudio == null) return NotFound();
        if (estudio.Plan != Planes.Prueba) { Error = "Solo se puede extender la prueba de un estudio con plan Prueba."; return RedirectToPage(); }

        // Se suman 30 días al vencimiento actual; si ya venció, se cuentan desde hoy.
        var actual = estudio.PruebaVence ?? estudio.CreadoEn.AddDays(Planes.DiasPrueba);
        var desde = actual > DateTime.UtcNow ? actual : DateTime.UtcNow;
        estudio.PruebaVence = desde.AddDays(Planes.DiasPrueba);
        db.Auditorias.Add(new AuditoriaEntry
        {
            EstudioId = estudio.Id, Usuario = tenant.DisplayName, Accion = "prueba_extendida",
            Detalle = $"Se extendió la prueba del estudio «{estudio.Nombre}» hasta {Fmt.FechaHora(estudio.PruebaVence.Value)}"
        });
        await db.SaveChangesAsync();
        Ok = $"Prueba de «{estudio.Nombre}» extendida hasta {Fmt.FechaHora(estudio.PruebaVence.Value)}.";
        return RedirectToPage();
    }

    private async Task CargarAsync()
    {
        // Estas consultas cruzan estudios a propósito: por eso se ignora el filtro de aislamiento.
        var empresas = await db.Empresas.IgnoreQueryFilters().AsNoTracking()
            .GroupBy(e => e.EstudioId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        var empleados = await db.Empleados.IgnoreQueryFilters().AsNoTracking().Where(e => e.Activo)
            .GroupBy(e => e.EstudioId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        var usuarios = await db.Users.AsNoTracking().Where(u => u.Activo)
            .GroupBy(u => u.EstudioId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);

        var lista = await db.Estudios.AsNoTracking().OrderBy(e => e.Nombre).ToListAsync();
        Estudios = lista.Select(e => new FilaEstudio
        {
            Id = e.Id, Nombre = e.Nombre, CreadoEn = e.CreadoEn, Plan = e.Plan,
            Prueba = Planes.CalcularPrueba(e.Plan, e.CreadoEn, e.PruebaVence, DateTime.UtcNow),
            Empresas = empresas.GetValueOrDefault(e.Id), Empleados = empleados.GetValueOrDefault(e.Id), Usuarios = usuarios.GetValueOrDefault(e.Id)
        }).ToList();
    }
}
