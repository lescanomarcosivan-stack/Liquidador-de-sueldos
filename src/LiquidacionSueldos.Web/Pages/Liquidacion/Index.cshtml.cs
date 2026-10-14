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

    /// <summary>Empleados incluidos cuyo neto da negativo (no se puede cerrar así).</summary>
    public List<string> NetoNegativo => Filas
        .Where(f => f.Input.Seleccionado && f.Calculo.Neto < 0)
        .Select(f => f.Empleado.NombreCompleto).ToList();

    /// <summary>Empleados incluidos que no tienen convenio: se liquidan sin antigüedad, presentismo ni aportes.</summary>
    public List<string> SinConvenio => Filas
        .Where(f => f.Input.Seleccionado && f.Empleado.ConvenioId == null)
        .Select(f => f.Empleado.NombreCompleto).ToList();

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

    /// <summary>Recalcula sin guardar: la pantalla lo llama (fetch) al editar novedades para actualizar los importes en vivo.</summary>
    public async Task<IActionResult> OnPostCalcularAsync()
    {
        Normalizar();
        var liq = await svc.BuscarVigenteAsync(Periodo, Tipo);
        if (liq is { EstaCerrada: true }) return new JsonResult(new { cerrada = true });

        var conceptos = await svc.ConceptosAsync();
        var empleados = (await svc.EmpleadosActivosAsync()).ToDictionary(e => e.Id);
        if (liq != null)
            foreach (var l in liq.Lineas.Where(l => l.Empleado != null && !empleados.ContainsKey(l.EmpleadoId)))
                empleados[l.EmpleadoId] = l.Empleado!;

        var filas = new List<object>();
        decimal rem = 0m, desc = 0m, neto = 0m;
        var cantidad = 0;
        foreach (var input in Lineas)
        {
            if (!empleados.TryGetValue(input.EmpleadoId, out var e)) continue;
            var previa = liq?.Lineas.FirstOrDefault(x => x.EmpleadoId == input.EmpleadoId);
            var tmp = new LiquidacionLinea
            {
                EmpleadoId = input.EmpleadoId, Seleccionado = input.Seleccionado, Dias = input.Dias,
                Ausencias = input.Ausencias, He50 = input.He50, He100 = input.He100,
                OtrosDescuentos = input.OtrosDescuentos, NoRemunerativo = input.NoRemunerativo,
                ConceptosJson = previa?.ConceptosJson
            };
            var c = LiquidacionService.CalcularLive(e, tmp, Periodo, conceptos);
            if (input.Seleccionado) { rem += c.Remunerativo; desc += c.Descuentos; neto += c.Neto; cantidad++; }
            filas.Add(new { id = input.EmpleadoId, rem = Fmt.Money(c.Remunerativo), desc = Fmt.Money(c.Descuentos), neto = Fmt.Money(c.Neto) });
        }
        return new JsonResult(new
        {
            filas,
            totales = new { empleados = cantidad, rem = Fmt.Money(rem), desc = Fmt.Money(desc), neto = Fmt.Money(neto) }
        });
    }

    /// <summary>Mensaje si algún campo de las novedades no es un número válido (letras, vacío, etc.); null si todo está bien.</summary>
    private string? ErrorDeLineas()
    {
        var malos = ModelState
            .Where(kv => kv.Key.StartsWith("Lineas", StringComparison.Ordinal) && kv.Value?.Errors.Count > 0)
            .Select(kv => kv.Key).ToList();
        return malos.Count == 0 ? null
            : "No se guardó: hay campos de novedades vacíos o con texto en lugar de números. Usá solo números (decimales con punto).";
    }

    public async Task<IActionResult> OnPostGuardarAsync()
    {
        Normalizar();
        if (ErrorDeLineas() is { } errLineas) { Error = errLineas; return RedirectToPage(new { Periodo, Tipo }); }
        if (FechaPago == default) FechaPago = UltimoDiaHabil(Periodo);
        var (_, error) = await svc.GuardarBorradorAsync(Periodo, Tipo, FechaPago, Lineas);
        if (error != null) Error = error; else Ok = "Borrador guardado.";
        return RedirectToPage(new { Periodo, Tipo });
    }

    public async Task<IActionResult> OnPostCerrarAsync()
    {
        Normalizar();
        if (ErrorDeLineas() is { } errLineas) { Error = errLineas; return RedirectToPage(new { Periodo, Tipo }); }
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
