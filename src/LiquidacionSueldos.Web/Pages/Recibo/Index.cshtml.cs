using LiquidacionSueldos.Web.Data;
using LiquidacionSueldos.Web.Domain;
using LiquidacionSueldos.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LiquidacionSueldos.Web.Pages.Recibo;

public class IndexModel(AppDbContext db, ITenantContext tenant, LiquidacionService svc, IAuditoria audit) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid? LiquidacionId { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? EmpleadoId { get; set; }
    [BindProperty(SupportsGet = true)] public bool Todos { get; set; }
    [BindProperty(SupportsGet = true)] public bool AutoPrint { get; set; }

    public List<Domain.Liquidacion> Liquidaciones { get; set; } = new();
    public Domain.Liquidacion? Liq { get; set; }
    public List<LiquidacionLinea> Lineas { get; set; } = new();
    public List<ReciboVm> Recibos { get; set; } = new();

    public async Task OnGetAsync()
    {
        var empresaId = tenant.RequireEmpresaId;
        Liquidaciones = await db.Liquidaciones.Where(l => l.EmpresaId == empresaId)
            .OrderByDescending(l => l.Periodo).ThenBy(l => l.Tipo).ThenByDescending(l => l.Version).ToListAsync();

        var id = LiquidacionId ?? Liquidaciones.FirstOrDefault()?.Id;
        if (id == null) return;

        Liq = await svc.ConLineas().FirstOrDefaultAsync(l => l.Id == id.Value && l.EmpresaId == empresaId);
        if (Liq == null) return;

        Lineas = Liq.Lineas.Where(l => l.Seleccionado && l.Empleado != null)
            .OrderBy(l => l.Empleado!.Apellido).ThenBy(l => l.Empleado!.Nombre).ToList();
        if (Lineas.Count == 0) return;

        var empresa = await db.Empresas.FirstAsync(e => e.Id == empresaId);
        var conceptos = await svc.ConceptosAsync();

        var elegidas = Todos
            ? Lineas
            : new List<LiquidacionLinea> { Lineas.FirstOrDefault(l => l.EmpleadoId == EmpleadoId) ?? Lineas[0] };
        if (!Todos) EmpleadoId = elegidas[0].EmpleadoId;
        Recibos = elegidas.Select(l => ReciboVm.Construir(Liq, l, empresa, conceptos)).ToList();
    }

    /// <summary>Registra la impresión en la auditoría y vuelve a la vista con la impresión automática.</summary>
    public async Task<IActionResult> OnPostImprimirAsync()
    {
        var empresaId = tenant.RequireEmpresaId;
        var liq = LiquidacionId.HasValue
            ? await db.Liquidaciones.FirstOrDefaultAsync(l => l.Id == LiquidacionId.Value && l.EmpresaId == empresaId)
            : null;
        if (liq == null) return RedirectToPage();

        string quien = "todos los recibos";
        if (!Todos && EmpleadoId.HasValue)
        {
            var emp = await db.Empleados.FirstOrDefaultAsync(e => e.Id == EmpleadoId.Value && e.EmpresaId == empresaId);
            quien = emp?.NombreCompleto ?? "un empleado";
        }
        audit.Registrar("recibo_impreso", $"Imprimió {quien} ({liq.Periodo} · {liq.Tipo}{(liq.Version > 1 ? $" v{liq.Version}" : "")})");
        await db.SaveChangesAsync();
        return RedirectToPage(new { LiquidacionId, EmpleadoId, Todos, AutoPrint = true });
    }
}
