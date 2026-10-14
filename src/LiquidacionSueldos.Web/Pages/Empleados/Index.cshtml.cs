using LiquidacionSueldos.Web.Data;
using LiquidacionSueldos.Web.Domain;
using LiquidacionSueldos.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LiquidacionSueldos.Web.Pages.Empleados;

public class IndexModel(AppDbContext db, ITenantContext tenant, IAuditoria audit) : PageModel
{
    public List<Empleado> Lista { get; set; } = new();
    public string EmpresaNombre { get; set; } = "";
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public bool Inactivos { get; set; }
    [BindProperty(SupportsGet = true)] public int Pagina { get; set; } = 1;
    public const int PorPagina = 50;
    public int Total { get; set; }
    public int TotalPaginas { get; set; } = 1;
    [TempData] public string? Ok { get; set; }
    [TempData] public string? Error { get; set; }

    public async Task OnGetAsync()
    {
        var empresaId = tenant.RequireEmpresaId;
        EmpresaNombre = (await db.Empresas.Where(e => e.Id == empresaId).Select(e => e.RazonSocial).FirstOrDefaultAsync()) ?? "";
        var q = db.Empleados.Include(e => e.Convenio).Include(e => e.Categoria).Where(e => e.EmpresaId == empresaId);
        if (!Inactivos) q = q.Where(e => e.Activo);
        if (!string.IsNullOrWhiteSpace(Q))
        {
            var t = Q.Trim().ToLower();
            q = q.Where(e => e.Apellido.ToLower().Contains(t) || e.Nombre.ToLower().Contains(t) ||
                             e.Legajo.ToLower().Contains(t) || e.Cuil.Contains(t));
        }
        Total = await q.CountAsync();
        TotalPaginas = Math.Max(1, (int)Math.Ceiling(Total / (double)PorPagina));
        Pagina = Math.Clamp(Pagina, 1, TotalPaginas);
        Lista = await q.AsNoTracking()
            .OrderBy(e => e.Apellido).ThenBy(e => e.Nombre).ThenBy(e => e.Id)
            .Skip((Pagina - 1) * PorPagina).Take(PorPagina)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostEliminarAsync(Guid id)
    {
        var empresaId = tenant.RequireEmpresaId;
        var e = await db.Empleados.FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId);
        if (e == null) return NotFound();

        if (await db.LiquidacionLineas.AnyAsync(l => l.EmpleadoId == id))
        {
            e.Activo = false;
            audit.Registrar("empleado_baja", $"Dio de baja a {e.NombreCompleto} (tiene liquidaciones, se conserva el historial)");
            Ok = "El empleado tiene liquidaciones: se dio de baja (inactivo) para conservar el historial.";
        }
        else
        {
            db.Empleados.Remove(e);
            audit.Registrar("empleado_eliminado", $"Eliminó al empleado {e.NombreCompleto}");
            Ok = "Empleado eliminado.";
        }
        await db.SaveChangesAsync();
        return RedirectToPage(new { Q, Inactivos });
    }
}
