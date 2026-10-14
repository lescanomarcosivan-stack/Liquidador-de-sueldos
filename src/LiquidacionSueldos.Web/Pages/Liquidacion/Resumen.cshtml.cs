using LiquidacionSueldos.Web.Data;
using LiquidacionSueldos.Web.Domain;
using LiquidacionSueldos.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LiquidacionSueldos.Web.Pages.Liquidacion;

public class FilaResumen
{
    public string Concepto { get; set; } = "";
    public decimal Importe { get; set; }
    /// <summary>Cantidad de empleados que tienen este concepto con importe distinto de cero.</summary>
    public int Empleados { get; set; }
}

public class SeccionResumen
{
    public string Titulo { get; set; } = "";
    public List<FilaResumen> Filas { get; set; } = new();
    public string TotalNombre { get; set; } = "";
    public decimal Total { get; set; }
}

public class LiquidacionIncluida
{
    public string Tipo { get; set; } = "";
    public int Version { get; set; }
    public bool Cerrada { get; set; }
    public int Empleados { get; set; }
}

/// <summary>Suma, para un período, cada concepto de todos los empleados (valores congelados si la liquidación está cerrada).</summary>
public class ResumenModel(AppDbContext db, ITenantContext tenant, LiquidacionService svc) : PageModel
{
    [BindProperty(SupportsGet = true)] public string Periodo { get; set; } = "";
    /// <summary>Vacío = todos los tipos de liquidación del período.</summary>
    [BindProperty(SupportsGet = true)] public string Tipo { get; set; } = "";

    public string EmpresaNombre { get; set; } = "";
    public List<LiquidacionIncluida> Incluidas { get; set; } = new();
    public List<SeccionResumen> Secciones { get; set; } = new();
    public int TotalEmpleados { get; set; }
    public decimal Bruto { get; set; }
    public decimal Descuentos { get; set; }
    public decimal Neto { get; set; }
    public SeccionResumen Patronales { get; set; } = new();
    public decimal CostoLaboral { get; set; }
    public bool HayBorradores => Incluidas.Any(i => !i.Cerrada);

    private static void Sumar(Dictionary<string, FilaResumen> d, string nombre, decimal valor)
    {
        if (valor == 0m) return;
        if (!d.TryGetValue(nombre, out var f)) d[nombre] = f = new FilaResumen { Concepto = nombre };
        f.Importe += valor;
        f.Empleados++;
    }

    private static List<FilaResumen> Ordenadas(Dictionary<string, FilaResumen> d, params string[] fijos)
    {
        var res = new List<FilaResumen>();
        foreach (var n in fijos) if (d.TryGetValue(n, out var f)) res.Add(f);
        res.AddRange(d.Values.Where(f => !fijos.Contains(f.Concepto)).OrderBy(f => f.Concepto));
        return res;
    }

    public async Task OnGetAsync()
    {
        if (!CalculoSueldos.TryParsePeriodo(Periodo, out _)) Periodo = Fmt.PeriodoActual();
        if (!string.IsNullOrEmpty(Tipo) && !IndexModel.Tipos.Contains(Tipo)) Tipo = "";

        var empresaId = tenant.RequireEmpresaId;
        var empresa = await db.Empresas.FirstOrDefaultAsync(e => e.Id == empresaId);
        EmpresaNombre = empresa?.RazonSocial ?? "";
        if (empresa == null) return;

        var delPeriodo = await svc.ConLineas()
            .Where(x => x.EmpresaId == empresaId && x.Periodo == Periodo)
            .ToListAsync();
        // Por cada tipo se toma la última versión (la rectificativa reemplaza a la original).
        var vigentes = delPeriodo
            .GroupBy(x => x.Tipo)
            .Select(g => g.OrderByDescending(x => x.Version).First())
            .Where(x => string.IsNullOrEmpty(Tipo) || x.Tipo == Tipo)
            .OrderBy(x => Array.IndexOf(IndexModel.Tipos, x.Tipo))
            .ToList();

        var conceptos = await svc.ConceptosAsync();

        var rem = new Dictionary<string, FilaResumen>();
        var noRem = new Dictionary<string, FilaResumen>();
        var desc = new Dictionary<string, FilaResumen>();
        var patr = new Dictionary<string, FilaResumen>();
        decimal totRem = 0m, totNoRem = 0m, totDesc = 0m, totNeto = 0m, totPatr = 0m;
        var empleadosUnicos = new HashSet<Guid>();

        foreach (var liq in vigentes)
        {
            var lineas = liq.Lineas.Where(l => l.Seleccionado && l.Empleado != null).ToList();
            Incluidas.Add(new LiquidacionIncluida { Tipo = liq.Tipo, Version = liq.Version, Cerrada = liq.EstaCerrada, Empleados = lineas.Count });

            foreach (var l in lineas)
            {
                empleadosUnicos.Add(l.EmpleadoId);
                var snap = LiquidacionService.LeerSnapshot(l);
                var c = snap?.Calculo ?? LiquidacionService.CalcularLive(l.Empleado!, l, liq.Periodo, conceptos);
                var contrib = snap?.Contribuciones ?? ContribucionesPatronales.Desde(empresa);
                var costo = CalculoSueldos.CalcularCostoLaboral(c, contrib);

                // Haberes remunerativos
                Sumar(rem, "Sueldo básico", c.Basico);
                Sumar(rem, "Antigüedad", c.Antiguedad);
                Sumar(rem, "Presentismo", c.Presentismo);
                Sumar(rem, "Horas extra 50%", c.He50);
                Sumar(rem, "Horas extra 100%", c.He100);
                foreach (var r in c.ConceptoRows.Where(r => r.Tipo == "rem")) Sumar(rem, r.Nombre, r.Valor);

                // No remunerativos: lo cargado como novedad + los conceptos configurados
                var noRemConceptos = c.ConceptoRows.Where(r => r.Tipo == "noRem").Sum(r => r.Valor);
                Sumar(noRem, "Conceptos no remunerativos (novedades)", c.NoRem - noRemConceptos);
                foreach (var r in c.ConceptoRows.Where(r => r.Tipo == "noRem")) Sumar(noRem, r.Nombre, r.Valor);

                // Descuentos
                Sumar(desc, "Jubilación (SIPA)", c.Jubilacion);
                Sumar(desc, "Obra social", c.ObraSocial);
                Sumar(desc, "INSSJP / Ley 19.032 (PAMI)", c.Inssjp);
                Sumar(desc, "Sindicato", c.Sindicato);
                Sumar(desc, "Otros descuentos", c.Otros);
                foreach (var r in c.ConceptoRows.Where(r => r.Tipo == "desc")) Sumar(desc, r.Nombre, r.Valor);

                // Contribuciones del empleador
                Sumar(patr, "Jubilación patronal (SIPA)", costo.ContribJub);
                Sumar(patr, "Obra social / PAMI patronal", costo.ContribOS);
                Sumar(patr, "A.R.T.", costo.ContribArt);
                Sumar(patr, "Asignaciones familiares", costo.ContribAsig);
                Sumar(patr, "Aporte sindical patronal", costo.ContribSind);

                totRem += c.Remunerativo;
                totNoRem += c.NoRem;
                totDesc += c.Descuentos;
                totNeto += c.Neto;
                totPatr += costo.ContribTotal;
            }
        }

        TotalEmpleados = empleadosUnicos.Count;
        Bruto = totRem + totNoRem;
        Descuentos = totDesc;
        Neto = totNeto;
        CostoLaboral = Bruto + totPatr;

        Secciones = new List<SeccionResumen>
        {
            new() { Titulo = "Haberes remunerativos", TotalNombre = "Total remunerativo", Total = totRem,
                Filas = Ordenadas(rem, "Sueldo básico", "Antigüedad", "Presentismo", "Horas extra 50%", "Horas extra 100%") },
            new() { Titulo = "Conceptos no remunerativos", TotalNombre = "Total no remunerativo", Total = totNoRem,
                Filas = Ordenadas(noRem, "Conceptos no remunerativos (novedades)") },
            new() { Titulo = "Descuentos", TotalNombre = "Total descuentos", Total = totDesc,
                Filas = Ordenadas(desc, "Jubilación (SIPA)", "Obra social", "INSSJP / Ley 19.032 (PAMI)", "Sindicato", "Otros descuentos") },
        };
        Patronales = new SeccionResumen
        {
            Titulo = "Contribuciones del empleador (costo adicional, no se descuentan)",
            TotalNombre = "Total contribuciones patronales", Total = totPatr,
            Filas = Ordenadas(patr, "Jubilación patronal (SIPA)", "Obra social / PAMI patronal", "A.R.T.", "Asignaciones familiares", "Aporte sindical patronal")
        };
    }
}
