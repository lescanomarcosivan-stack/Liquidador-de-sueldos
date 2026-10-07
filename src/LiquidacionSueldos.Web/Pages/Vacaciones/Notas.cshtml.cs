using LiquidacionSueldos.Web.Data;
using LiquidacionSueldos.Web.Domain;
using LiquidacionSueldos.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LiquidacionSueldos.Web.Pages.Vacaciones;

public class NotasModel(AppDbContext db, ITenantContext tenant, IAuditoria audit) : PageModel
{
    public class NotaInput
    {
        public string Tipo { get; set; } = "vacaciones";
        public Guid? EmpleadoId { get; set; }
        public string? Periodo { get; set; }
        public DateOnly? Inicio { get; set; }
        public int Dias { get; set; }
        public string? Obs { get; set; }
        public DateOnly? Desde { get; set; }
        public DateOnly? Hasta { get; set; }
        public string? Articulo { get; set; }
        public string? Motivo { get; set; }
    }

    [BindProperty] public NotaInput Input { get; set; } = new();
    public List<Empleado> Empleados { get; set; } = new();
    public List<Nota> Historial { get; set; } = new();
    public Empleado? Empleado { get; set; }
    public Empresa? Empresa { get; set; }
    public DateOnly? Fin => Input.Inicio.HasValue && Input.Dias > 0 ? Input.Inicio.Value.AddDays(Input.Dias - 1) : null;
    [TempData] public string? Ok { get; set; }
    [TempData] public string? Error { get; set; }

    public async Task OnGetAsync()
    {
        Input.Periodo = Fmt.PeriodoActual();
        Input.Inicio = Fmt.HoyAr();
        Input.Desde = Fmt.HoyAr();
        Input.Hasta = Fmt.HoyAr();
        await CargarAsync();
    }

    private async Task CargarAsync()
    {
        var empresaId = tenant.RequireEmpresaId;
        Empresa = await db.Empresas.FirstOrDefaultAsync(e => e.Id == empresaId);
        Empleados = await db.Empleados.Where(e => e.EmpresaId == empresaId && e.Activo)
            .OrderBy(e => e.Apellido).ThenBy(e => e.Nombre).ToListAsync();
        Empleado = Empleados.FirstOrDefault(e => e.Id == Input.EmpleadoId) ?? Empleados.FirstOrDefault();
        Input.EmpleadoId = Empleado?.Id;
        if (Input.Tipo != "suspension") Input.Tipo = "vacaciones";
        Historial = await db.Notas.Include(n => n.Empleado).Where(n => n.EmpresaId == empresaId)
            .OrderByDescending(n => n.Fecha).ThenByDescending(n => n.Id).ToListAsync();
    }

    /// <summary>Actualiza la vista previa con los datos del formulario.</summary>
    public async Task OnPostAsync() => await CargarAsync();

    public async Task<IActionResult> OnPostGuardarAsync()
    {
        await CargarAsync();
        if (Empleado == null) { Error = "Seleccioná un empleado."; return Page(); }
        if (Input.Tipo == "vacaciones" && (!Input.Inicio.HasValue || Input.Dias <= 0))
        { Error = "Completá la fecha de inicio y los días otorgados."; return Page(); }
        if (Input.Tipo == "suspension" && (!Input.Desde.HasValue || !Input.Hasta.HasValue || string.IsNullOrWhiteSpace(Input.Motivo)))
        { Error = "Completá las fechas y el motivo de la suspensión."; return Page(); }

        db.Notas.Add(new Nota
        {
            EstudioId = tenant.EstudioId, EmpresaId = Empleado.EmpresaId, EmpleadoId = Empleado.Id, Fecha = Fmt.HoyAr(),
            Tipo = Input.Tipo, Periodo = Input.Periodo, Inicio = Input.Inicio, Dias = Input.Dias, Obs = Input.Obs?.Trim(),
            Desde = Input.Desde, Hasta = Input.Hasta, Articulo = Input.Articulo?.Trim(), Motivo = Input.Motivo?.Trim()
        });
        audit.Registrar("nota_emitida",
            $"Emitió nota de {(Input.Tipo == "vacaciones" ? "vacaciones" : "suspensión")} para {Empleado.NombreCompleto}");
        await db.SaveChangesAsync();
        Ok = "Nota guardada en el historial.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEliminarAsync(Guid id)
    {
        var empresaId = tenant.RequireEmpresaId;
        var n = await db.Notas.FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId);
        if (n == null) return NotFound();
        db.Notas.Remove(n);
        audit.Registrar("nota_eliminada", "Eliminó una nota del historial");
        await db.SaveChangesAsync();
        Ok = "Nota eliminada.";
        return RedirectToPage();
    }

    public static string Resumen(Nota n) => n.Tipo == "vacaciones"
        ? $"Período {n.Periodo ?? "—"} · {n.Dias} días desde {Fmt.Fecha(n.Inicio)}"
        : $"Del {Fmt.Fecha(n.Desde)} al {Fmt.Fecha(n.Hasta)}";
}
