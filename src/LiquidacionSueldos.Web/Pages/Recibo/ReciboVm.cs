using LiquidacionSueldos.Web.Domain;
using LiquidacionSueldos.Web.Services;

namespace LiquidacionSueldos.Web.Pages.Recibo;

public record PieSlice(string Label, decimal Value, string Color);

/// <summary>Todo lo que necesita una copia del recibo, venga de la foto congelada o del cálculo en vivo.</summary>
public class ReciboVm
{
    public bool Congelado { get; set; }
    public string Periodo { get; set; } = "";
    public string Tipo { get; set; } = "";
    public DateOnly FechaPago { get; set; }
    public SnapshotEmpleador Empleador { get; set; } = new();
    public SnapshotEmpleado Empleado { get; set; } = new();
    public string Convenio { get; set; } = "";
    public string Categoria { get; set; } = "";
    public ResultadoCalculo Calculo { get; set; } = new();
    public ParametrosConvenio Parametros { get; set; } = new();
    public ContribucionesPatronales Contribuciones { get; set; } = new();
    public CostoLaboral Costo { get; set; } = new();

    public static readonly string[] Colores = ["#28724b", "#3d5a80", "#b5720d", "#7d5ba6", "#a6373a", "#2f8f9d", "#8a8f36"];

    /// <summary>Filas (concepto, importe, remunerativo) de la tabla de haberes.</summary>
    public List<(string Nombre, decimal Valor, bool Rem)> Haberes()
    {
        var c = Calculo;
        var r = new List<(string, decimal, bool)>
        {
            ("Sueldo básico", c.Basico, true), ("Antigüedad", c.Antiguedad, true), ("Presentismo", c.Presentismo, true)
        };
        if (c.He50 != 0) r.Add(("Horas extra 50%", c.He50, true));
        if (c.He100 != 0) r.Add(("Horas extra 100%", c.He100, true));
        foreach (var x in c.ConceptoRows.Where(x => x.Tipo != "desc")) r.Add((x.Nombre, x.Valor, x.Tipo == "rem"));
        if (c.NoRem != 0) r.Add(("Conceptos no remunerativos", c.NoRem, false));
        return r;
    }

    public List<(string Nombre, decimal Valor)> Descuentos()
    {
        var c = Calculo;
        var r = new List<(string, decimal)>
        {
            ("Jubilación (SIPA)", c.Jubilacion), ("Obra social", c.ObraSocial),
            ("INSSJP / Ley 19.032 (PAMI)", c.Inssjp), ("Sindicato", c.Sindicato)
        };
        r = r.Where(x => x.Item2 > 0).ToList();
        r.AddRange(c.ConceptoRows.Where(x => x.Tipo == "desc").Select(x => (x.Nombre, x.Valor)));
        if (c.Otros > 0) r.Add(("Otros descuentos", c.Otros));
        return r;
    }

    public List<(string Nombre, decimal Alicuota, decimal Valor)> Aportes()
    {
        var c = Calculo; var p = Parametros;
        return new List<(string, decimal, decimal)>
        {
            ("Jubilación (SIPA)", p.Jubilacion, c.Jubilacion), ("Obra social", p.ObraSocial, c.ObraSocial),
            ("INSSJP / Ley 19.032 (PAMI)", p.Inssjp, c.Inssjp), ("Sindicato", p.Sindicato, c.Sindicato)
        }.Where(x => x.Item3 > 0).ToList();
    }

    public List<(string Nombre, decimal Alicuota, decimal Valor)> Patronales()
    {
        var cc = Contribuciones; var k = Costo;
        return new List<(string, decimal, decimal)>
        {
            ("Jubilación (SIPA)", cc.JubilacionPatronal, k.ContribJub), ("Obra social / PAMI", cc.ObraSocialPatronal, k.ContribOS),
            ("A.R.T.", cc.Art, k.ContribArt), ("Asignaciones familiares", cc.AsignacionesFamiliares, k.ContribAsig),
            ("Sindical / cámara", cc.SindicalPatronal, k.ContribSind)
        }.Where(x => x.Item3 > 0).ToList();
    }

    public List<PieSlice> Torta()
    {
        var k = Costo; var c = Calculo;
        return new List<PieSlice>
        {
            new("Neto trabajador", c.Neto, Colores[0]),
            new("Aportes del trabajador", c.Descuentos, Colores[1]),
            new("Jubilación patronal (SIPA)", k.ContribJub, Colores[2]),
            new("Obra social / PAMI patronal", k.ContribOS, Colores[3]),
            new("A.R.T.", k.ContribArt, Colores[4]),
            new("Asignaciones familiares", k.ContribAsig, Colores[5]),
            new("Sindical / cámara patronal", k.ContribSind, Colores[6])
        }.Where(s => s.Value > 0.005m).ToList();
    }

    /// <summary>Traza el SVG de la torta (mismo algoritmo que el prototipo HTML).</summary>
    public static string TortaSvg(IReadOnlyList<PieSlice> slices, int size)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var total = slices.Sum(s => Math.Max(0m, s.Value));
        if (total <= 0) total = 1;
        double r = size / 2.0, cx = r, cy = r, angle = 0;
        var sb = new System.Text.StringBuilder();
        sb.Append(string.Format(inv, "<svg viewBox=\"0 0 {0} {0}\" width=\"{0}\" height=\"{0}\">", size));
        foreach (var s in slices)
        {
            var portion = (double)(s.Value / total) * 360.0;
            var start = angle; var end = angle + portion; angle = end;
            if (end - start >= 359.999) end = start + 359.999;
            var large = end - start > 180 ? 1 : 0;
            double Rad(double a) => Math.PI / 180 * (a - 90);
            var x1 = cx + r * Math.Cos(Rad(start)); var y1 = cy + r * Math.Sin(Rad(start));
            var x2 = cx + r * Math.Cos(Rad(end)); var y2 = cy + r * Math.Sin(Rad(end));
            sb.Append(string.Format(inv,
                "<path d=\"M{0},{1} L{2:0.00},{3:0.00} A{0},{0} 0 {4} 1 {5:0.00},{6:0.00} Z\" fill=\"{7}\" stroke=\"#fff\" stroke-width=\"0.6\"/>",
                r, r, x1, y1, large, x2, y2, s.Color));
        }
        sb.Append("</svg>");
        return sb.ToString();
    }

    public static ReciboVm Construir(Domain.Liquidacion liq, LiquidacionLinea linea, Empresa empresa, IEnumerable<ConceptoCalc> conceptos)
    {
        var snap = LiquidacionService.LeerSnapshot(linea);
        var e = linea.Empleado!;
        var vm = new ReciboVm { Periodo = liq.Periodo, Tipo = liq.Tipo, FechaPago = liq.FechaPago };
        if (snap != null)
        {
            vm.Congelado = true;
            vm.Calculo = snap.Calculo;
            vm.Empleador = snap.Empleador;
            vm.Empleado = snap.Empleado;
            vm.Convenio = snap.ConvenioNombre;
            vm.Categoria = snap.CategoriaNombre;
            vm.Parametros = snap.Parametros;
            vm.Contribuciones = snap.Contribuciones;
        }
        else
        {
            vm.Calculo = LiquidacionService.CalcularLive(e, linea, liq.Periodo, conceptos);
            vm.Empleador = new SnapshotEmpleador
            {
                RazonSocial = empresa.RazonSocial, Cuit = empresa.Cuit, Domicilio = empresa.Domicilio, Lugar = empresa.Lugar,
                Leyenda = string.IsNullOrWhiteSpace(empresa.Leyenda) ? "Recibo de haberes" : empresa.Leyenda,
                LogoDataUrl = empresa.LogoDataUrl
            };
            vm.Empleado = new SnapshotEmpleado
            {
                Nombre = e.NombreCompleto, Legajo = e.Legajo, Cuil = e.Cuil, Dni = e.Dni, FechaIngreso = Fmt.FechaIso(e.FechaIngreso)
            };
            vm.Convenio = e.Convenio?.Nombre ?? "Sin convenio";
            vm.Categoria = e.Categoria?.Nombre ?? "";
            vm.Parametros = ParametrosConvenio.Desde(e.Convenio);
            vm.Contribuciones = ContribucionesPatronales.Desde(empresa);
        }
        vm.Costo = CalculoSueldos.CalcularCostoLaboral(vm.Calculo, vm.Contribuciones);
        return vm;
    }
}
