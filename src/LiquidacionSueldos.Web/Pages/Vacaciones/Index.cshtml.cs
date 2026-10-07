using LiquidacionSueldos.Web.Data;
using LiquidacionSueldos.Web.Domain;
using LiquidacionSueldos.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LiquidacionSueldos.Web.Pages.Vacaciones;

public class IndexModel(AppDbContext db, ITenantContext tenant, IAuditoria audit) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid? EmpleadoId { get; set; }
    [BindProperty(SupportsGet = true)] public string Periodo { get; set; } = "";
    [BindProperty(SupportsGet = true)] public DateOnly? Inicio { get; set; }
    [BindProperty(SupportsGet = true)] public int? Dias { get; set; }

    public List<Empleado> Empleados { get; set; } = new();
    public List<Vacacion> Historial { get; set; } = new();
    public Empleado? Empleado { get; set; }
    public int Anios { get; set; }
    public int DiasSugeridos { get; set; }
    public int DiasAOtorgar { get; set; }
    public decimal Base { get; set; }
    public decimal ValorDia { get; set; }
    public decimal Importe { get; set; }
    [TempData] public string? Ok { get; set; }
    [TempData] public string? Error { get; set; }

    public async Task OnGetAsync()
    {
        await CargarAsync();
    }

    private async Task CargarAsync()
    {
        var empresaId = tenant.RequireEmpresaId;
        if (!CalculoSueldos.TryParsePeriodo(Periodo, out _)) Periodo = Fmt.PeriodoActual();
        Inicio ??= Fmt.HoyAr();

        Empleados = await db.Empleados.Include(e => e.Convenio).Include(e => e.Categoria)
            .Where(e => e.EmpresaId == empresaId && e.Activo)
            .OrderBy(e => e.Apellido).ThenBy(e => e.Nombre).ToListAsync();
        Empleado = Empleados.FirstOrDefault(e => e.Id == EmpleadoId) ?? Empleados.FirstOrDefault();
        EmpleadoId = Empleado?.Id;

        if (Empleado != null)
        {
            Anios = CalculoSueldos.AniosAntiguedad(Empleado.FechaIngreso, Periodo);
            DiasSugeridos = CalculoSueldos.DiasVacaciones(Empleado.Convenio, Anios);
            DiasAOtorgar = Math.Max(0, Dias ?? DiasSugeridos);
            Base = Empleado.BasicoEfectivo;
            ValorDia = CalculoSueldos.R(Base / 25m);
            Importe = CalculoSueldos.ImporteVacaciones(Base, DiasAOtorgar);
        }

        Historial = await db.Vacaciones.Include(v => v.Empleado)
            .Where(v => v.EmpresaId == empresaId)
            .OrderByDescending(v => v.Inicio).ToListAsync();
    }

    public async Task<IActionResult> OnPostGuardarAsync()
    {
        await CargarAsync();
        if (Empleado == null) { Error = "Seleccioná un empleado."; return RedirectToPage(); }
        if (DiasAOtorgar <= 0) { Error = "Los días de vacaciones deben ser mayores a cero."; return RedirectToPage(new { EmpleadoId, Periodo }); }

        db.Vacaciones.Add(new Vacacion
        {
            EstudioId = tenant.EstudioId, EmpresaId = Empleado.EmpresaId, EmpleadoId = Empleado.Id, Periodo = Periodo,
            Inicio = Inicio!.Value, Dias = DiasAOtorgar, BaseMensual = Base, Importe = Importe, FechaRegistro = Fmt.HoyAr()
        });
        audit.Registrar("vacaciones_registradas",
            $"Registró {DiasAOtorgar} días de vacaciones para {Empleado.NombreCompleto} ({Periodo}) por {Fmt.Money(Importe)}");
        await db.SaveChangesAsync();
        Ok = "Licencia registrada.";
        return RedirectToPage(new { EmpleadoId, Periodo });
    }

    public async Task<IActionResult> OnPostEliminarAsync(Guid id)
    {
        var empresaId = tenant.RequireEmpresaId;
        var v = await db.Vacaciones.Include(x => x.Empleado).FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId);
        if (v == null) return NotFound();
        db.Vacaciones.Remove(v);
        audit.Registrar("vacaciones_eliminadas", $"Eliminó el registro de vacaciones de {v.Empleado?.NombreCompleto} ({v.Periodo})");
        await db.SaveChangesAsync();
        Ok = "Registro eliminado.";
        return RedirectToPage();
    }
}
