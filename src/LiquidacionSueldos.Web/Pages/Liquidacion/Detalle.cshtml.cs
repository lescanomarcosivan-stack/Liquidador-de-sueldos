using System.Text.Json;
using LiquidacionSueldos.Web.Data;
using LiquidacionSueldos.Web.Domain;
using LiquidacionSueldos.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LiquidacionSueldos.Web.Pages.Liquidacion;

public class DetalleModel(AppDbContext db, ITenantContext tenant, LiquidacionService svc, IAuditoria audit) : PageModel
{
    [BindProperty(SupportsGet = true)] public string Periodo { get; set; } = "";
    [BindProperty(SupportsGet = true)] public string Tipo { get; set; } = "Mensual";
    [BindProperty(SupportsGet = true)] public Guid EmpleadoId { get; set; }

    public Domain.Liquidacion? Liq { get; set; }
    public LiquidacionLinea? Linea { get; set; }
    public ResultadoCalculo Calculo { get; set; } = new();
    public List<ConceptoCalc> Conceptos { get; set; } = new();
    public Dictionary<string, decimal> Overrides { get; set; } = new();
    public Dictionary<string, AgregadoCalc> Agregados { get; set; } = new();
    public CostoLaboral Costo { get; set; } = new();
    [TempData] public string? Ok { get; set; }
    [TempData] public string? Error { get; set; }
    public bool Cerrada => Liq?.EstaCerrada == true;

    public async Task<IActionResult> OnGetAsync()
    {
        return await CargarAsync() ? Page() : NotFound();
    }

    private async Task<bool> CargarAsync()
    {
        Liq = await svc.BuscarVigenteAsync(Periodo, Tipo);
        Linea = Liq?.Lineas.FirstOrDefault(l => l.EmpleadoId == EmpleadoId);
        if (Liq == null || Linea?.Empleado == null) return false;

        Conceptos = await svc.ConceptosAsync();
        var nov = LiquidacionService.Novedades(Linea);
        Overrides = nov.Overrides;
        Agregados = nov.Agregados;
        Calculo = LiquidacionService.Resultado(Liq, Linea, Conceptos);

        var empresa = await db.Empresas.FirstOrDefaultAsync(e => e.Id == Liq.EmpresaId);
        var snap = LiquidacionService.LeerSnapshot(Linea);
        var contrib = snap?.Contribuciones ?? (empresa != null ? ContribucionesPatronales.Desde(empresa) : new ContribucionesPatronales());
        Costo = CalculoSueldos.CalcularCostoLaboral(Calculo, contrib);
        return true;
    }

    public static readonly string[] BasesValidas = { "fijo", "pctBasico", "pctHaberes", "pctRemunerativo", "pctNoRemunerativo", "pctBruto" };

    /// <summary>Agrega a este empleado un concepto, eligiendo la base de cálculo y el valor para él.</summary>
    public async Task<IActionResult> OnPostAgregarAsync(Guid conceptoId, string calculo, decimal valor)
    {
        if (!await CargarAsync()) return NotFound();
        var volver = RedirectToPage(new { Periodo, Tipo, EmpleadoId });
        if (Cerrada) { Error = "La liquidación está cerrada."; return volver; }

        var con = Conceptos.FirstOrDefault(c => c.Id == conceptoId.ToString());
        if (con == null) { Error = "Elegí un concepto."; return volver; }
        if (!BasesValidas.Contains(calculo)) calculo = "fijo";
        if (con.Tipo != "descuento" && calculo is "pctRemunerativo" or "pctNoRemunerativo" or "pctBruto")
        {
            Error = "Un haber no puede calcularse sobre el total remunerativo, no remunerativo o bruto; solo los descuentos.";
            return volver;
        }
        valor = Math.Max(0m, valor);
        if (calculo != "fijo") valor = Math.Min(valor, 1000m) / 100m;

        var lista = LiquidacionService.AgregadosDe(Linea!);
        lista.RemoveAll(a => a.Id == con.Id);
        lista.Add(new AgregadoCalc { Id = con.Id, Calculo = calculo, Valor = Math.Round(valor, 6) });
        Linea!.ConceptosAgregadosJson = JsonSerializer.Serialize(lista);
        audit.Registrar("liquidacion_concepto_agregado",
            $"Agregó «{con.Nombre}» a {Linea.Empleado!.NombreCompleto} en {Periodo} · {Tipo}");
        await db.SaveChangesAsync();
        Ok = $"Se agregó «{con.Nombre}».";
        return volver;
    }

    public async Task<IActionResult> OnPostQuitarAsync(Guid conceptoId)
    {
        if (!await CargarAsync()) return NotFound();
        var volver = RedirectToPage(new { Periodo, Tipo, EmpleadoId });
        if (Cerrada) { Error = "La liquidación está cerrada."; return volver; }

        var lista = LiquidacionService.AgregadosDe(Linea!);
        var id = conceptoId.ToString();
        if (lista.RemoveAll(a => a.Id == id) > 0)
        {
            Linea!.ConceptosAgregadosJson = lista.Count == 0 ? null : JsonSerializer.Serialize(lista);
            audit.Registrar("liquidacion_concepto_quitado", $"Quitó un concepto agregado de {Linea.Empleado!.NombreCompleto} en {Periodo} · {Tipo}");
            await db.SaveChangesAsync();
            Ok = "Concepto quitado.";
        }
        return volver;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await CargarAsync()) return NotFound();
        if (Cerrada) { Error = "La liquidación está cerrada."; return RedirectToPage(new { Periodo, Tipo, EmpleadoId }); }

        var dict = new Dictionary<string, decimal>();
        // Los conceptos a elección no tienen campo en este formulario: se conservan los importes que ya tuvieran cargados.
        foreach (var c in Conceptos.Where(c => c.Manual))
            if (Overrides.TryGetValue(c.Id, out var previo)) dict[c.Id] = previo;
        foreach (var c in Conceptos.Where(c => !c.Manual))
        {
            var raw = Request.Form[$"ov_{c.Id}"].ToString().Trim();
            if (raw.Length == 0) continue;
            if (decimal.TryParse(raw, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var v) && v >= 0)
                dict[c.Id] = v;
        }
        Linea!.ConceptosJson = dict.Count == 0 ? null : JsonSerializer.Serialize(dict);
        audit.Registrar("liquidacion_concepto_manual",
            $"Ajustó conceptos de {Linea.Empleado!.NombreCompleto} en {Periodo} · {Tipo}");
        await db.SaveChangesAsync();
        Ok = "Conceptos guardados.";
        return RedirectToPage(new { Periodo, Tipo, EmpleadoId });
    }
}
