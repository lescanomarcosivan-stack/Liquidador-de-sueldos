using LiquidacionSueldos.Web.Data;
using LiquidacionSueldos.Web.Domain;
using LiquidacionSueldos.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LiquidacionSueldos.Web.Pages.Liquidacion;

public class FilaLiquidacion
{
    public Empleado Empleado { get; set; } = null!;
    public LineaInput Input { get; set; } = new();
    public ResultadoCalculo Calculo { get; set; } = new();
    public bool EnLiquidacion { get; set; }
}

public class IndexModel(AppDbContext db, ITenantContext tenant, LiquidacionService svc) : PageModel
{
    public static readonly string[] Tipos = ["Mensual", "Quincenal", "Adelanto", "Ajuste", "Final"];

    [BindProperty(SupportsGet = true)] public string Periodo { get; set; } = "";
    [BindProperty(SupportsGet = true)] public string Tipo { get; set; } = "Mensual";
    [BindProperty] public DateOnly FechaPago { get; set; }
    [BindProperty] public List<LineaInput> Lineas { get; set; } = new();

    public Domain.Liquidacion? Liq { get; set; }
    public List<FilaLiquidacion> Filas { get; set; } = new();
    public string EmpresaNombre { get; set; } = "";
    [TempData] public string? Ok { get; set; }
    [TempData] public string? Error { get; set; }

    public bool Cerrada => Liq?.EstaCerrada == true;
    public decimal TotalRem => Filas.Where(f => f.Input.Seleccionado).Sum(f => f.Calculo.Remunerativo);
    public decimal TotalDesc => Filas.Where(f => f.Input.Seleccionado).Sum(f => f.Calculo.Descuentos);
    public decimal TotalNeto => Filas.Where(f => f.Input.Seleccionado).Sum(f => f.Calculo.Neto);
    public int TotalEmpleados => Filas.Count(f => f.Input.Seleccionado);

    private void Normalizar()
    {
        if (!CalculoSueldos.TryParsePeriodo(Periodo, out _)) Periodo = Fmt.PeriodoActual();
        if (!Tipos.Contains(Tipo)) Tipo = "Mensual";
    }

    public async Task OnGetAsync()
    {
        Normalizar();
        await CargarAsync();
    }

    private async Task CargarAsync()
    {
        var empresaId = tenant.RequireEmpresaId;
        EmpresaNombre = (await db.Empresas.Where(e => e.Id == empresaId).Select(e => e.RazonSocial).FirstOrDefaultAsync()) ?? "";
        Liq = await svc.BuscarVigenteAsync(Periodo, Tipo);
        FechaPago = Liq?.FechaPago ?? UltimoDiaHabil(Periodo);

        var conceptos = await svc.ConceptosAsync();
        var activos = await svc.EmpleadosActivosAsync();
        var empleados = activos.ToList();
        if (Liq != null)
            foreach (var l in Liq.Lineas.Where(l => l.Empleado != null && empleados.All(e => e.Id != l.EmpleadoId)))
                empleados.Add(l.Empleado!);

        Filas = new();
        foreach (var e in empleados.OrderBy(e => e.Apellido).ThenBy(e => e.Nombre))
        {
            var linea = Liq?.Lineas.FirstOrDefault(l => l.EmpleadoId == e.Id);
            var tmp = linea ?? new LiquidacionLinea { EmpleadoId = e.Id, Seleccionado = Liq == null };
            var fila = new FilaLiquidacion
            {
                Empleado = e, EnLiquidacion = linea != null,
                Input = new LineaInput
                {
                    EmpleadoId = e.Id, Seleccionado = tmp.Seleccionado, Dias = tmp.Dias, Ausencias = tmp.Ausencias,
                    He50 = tmp.He50, He100 = tmp.He100, OtrosDescuentos = tmp.OtrosDescuentos, NoRemunerativo = tmp.NoRemunerativo
                }
            };
            if (Cerrada && linea != null && LiquidacionService.LeerSnapshot(linea) is { } snap) fila.Calculo = snap.Calculo;
            else fila.Calculo = LiquidacionService.CalcularLive(e, tmp, Periodo, conceptos);
            Filas.Add(fila);
        }
    }

    private static DateOnly UltimoDiaHabil(string periodo)
    {
        if (!CalculoSueldos.TryParsePeriodo(periodo, out var primero)) return Fmt.HoyAr();
        var d = new DateOnly(primero.Year, primero.Month, DateTime.DaysInMonth(primero.Year, primero.Month));
        while (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) d = d.AddDays(-1);
        return d;
    }

    public async Task<IActionResult> OnPostGuardarAsync()
    {
        Normalizar();
        if (FechaPago == default) FechaPago = UltimoDiaHabil(Periodo);
        var (_, error) = await svc.GuardarBorradorAsync(Periodo, Tipo, FechaPago, Lineas);
        if (error != null) Error = error; else Ok = "Borrador guardado.";
        return RedirectToPage(new { Periodo, Tipo });
    }

    public async Task<IActionResult> OnPostCerrarAsync()
    {
        Normalizar();
        if (FechaPago == default) FechaPago = UltimoDiaHabil(Periodo);
        var (liq, error) = await svc.GuardarBorradorAsync(Periodo, Tipo, FechaPago, Lineas);
        if (error == null && liq != null) error = await svc.CerrarAsync(liq);
        if (error != null) Error = error; else Ok = "Liquidación cerrada. Los recibos quedaron congelados.";
        return RedirectToPage(new { Periodo, Tipo });
    }

    public async Task<IActionResult> OnPostRectificarAsync(Guid id)
    {
        Normalizar();
        var (_, error) = await svc.CrearRectificativaAsync(id);
        if (error != null) Error = error; else Ok = "Se creó la rectificativa en borrador. Corregí las novedades y volvé a cerrarla.";
        return RedirectToPage(new { Periodo, Tipo });
    }
}
