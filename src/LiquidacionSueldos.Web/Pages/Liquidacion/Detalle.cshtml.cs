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
        Overrides = LiquidacionService.Novedades(Linea).Overrides;
        Calculo = LiquidacionService.Resultado(Liq, Linea, Conceptos);

        var empresa = await db.Empresas.FirstOrDefaultAsync(e => e.Id == Liq.EmpresaId);
        var snap = LiquidacionService.LeerSnapshot(Linea);
        var contrib = snap?.Contribuciones ?? (empresa != null ? ContribucionesPatronales.Desde(empresa) : new ContribucionesPatronales());
        Costo = CalculoSueldos.CalcularCostoLaboral(Calculo, contrib);
        return true;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await CargarAsync()) return NotFound();
        if (Cerrada) { Error = "La liquidación está cerrada."; return RedirectToPage(new { Periodo, Tipo, EmpleadoId }); }

        var dict = new Dictionary<string, decimal>();
        foreach (var c in Conceptos)
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
