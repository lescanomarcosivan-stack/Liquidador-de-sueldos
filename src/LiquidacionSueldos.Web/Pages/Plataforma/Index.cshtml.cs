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
        db.Auditorias.Add(new AuditoriaEntry
        {
            EstudioId = estudio.Id, Usuario = tenant.DisplayName, Accion = "plan_cambiado",
            Detalle = $"Plan del estudio «{estudio.Nombre}»: {anterior} → {estudio.Plan}"
        });
        await db.SaveChangesAsync();
        Ok = $"«{estudio.Nombre}» ahora tiene el plan {Planes.Obtener(estudio.Plan).Nombre}.";
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
            Empresas = empresas.GetValueOrDefault(e.Id), Empleados = empleados.GetValueOrDefault(e.Id), Usuarios = usuarios.GetValueOrDefault(e.Id)
        }).ToList();
    }
}
