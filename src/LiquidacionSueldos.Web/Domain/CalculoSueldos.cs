namespace LiquidacionSueldos.Web.Domain;

/// <summary>Parámetros del convenio usados para calcular (todos como fracciones; He50/He100 son multiplicadores).</summary>
public class ParametrosConvenio
{
    public decimal Antiguedad { get; set; }
    public decimal Presentismo { get; set; }
    public decimal He50 { get; set; } = 1.5m;
    public decimal He100 { get; set; } = 2m;
    public decimal Jubilacion { get; set; }
    public decimal Inssjp { get; set; }
    public decimal ObraSocial { get; set; }
    public decimal Sindicato { get; set; }

    public static ParametrosConvenio Desde(Convenio? c) => c == null
        ? new ParametrosConvenio()
        : new ParametrosConvenio
        {
            Antiguedad = c.Antiguedad, Presentismo = c.Presentismo, He50 = c.He50, He100 = c.He100,
            Jubilacion = c.Jubilacion, Inssjp = c.Inssjp, ObraSocial = c.ObraSocial, Sindicato = c.Sindicato
        };
}

public class ConceptoCalc
{
    public string Id { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Tipo { get; set; } = "haber";
    public bool Remunerativo { get; set; } = true;
    public string Calculo { get; set; } = "fijo";
    public decimal Valor { get; set; }
}

public class NovedadesCalc
{
    public decimal Dias { get; set; } = 30;
    public decimal Ausencias { get; set; }
    public decimal He50 { get; set; }
    public decimal He100 { get; set; }
    public decimal OtrosDescuentos { get; set; }
    public decimal NoRemunerativo { get; set; }
    /// <summary>Importes que reemplazan el valor automático de un concepto (clave = Id del concepto).</summary>
    public Dictionary<string, decimal> Overrides { get; set; } = new();
}

public class ConceptoLinea
{
    public string Nombre { get; set; } = "";
    public decimal Valor { get; set; }
    /// <summary>"rem", "noRem" o "desc".</summary>
    public string Tipo { get; set; } = "rem";
    public bool Auto { get; set; } = true;
}

public class ResultadoCalculo
{
    public decimal Basico { get; set; }
    public decimal Antiguedad { get; set; }
    public decimal Presentismo { get; set; }
    public decimal He50 { get; set; }
    public decimal He100 { get; set; }
    public decimal RemExtras { get; set; }
    public decimal Remunerativo { get; set; }
    public decimal Jubilacion { get; set; }
    public decimal Inssjp { get; set; }
    public decimal ObraSocial { get; set; }
    public decimal Sindicato { get; set; }
    public decimal Otros { get; set; }
    public decimal DescExtras { get; set; }
    public decimal Descuentos { get; set; }
    public decimal NoRem { get; set; }
    public decimal Neto { get; set; }
    public int Anios { get; set; }
    public decimal ValorHora { get; set; }
    public List<ConceptoLinea> ConceptoRows { get; set; } = new();
}

public class CostoLaboral
{
    public decimal ContribJub { get; set; }
    public decimal ContribOS { get; set; }
    public decimal ContribArt { get; set; }
    public decimal ContribAsig { get; set; }
    public decimal ContribSind { get; set; }
    public decimal ContribTotal { get; set; }
    public decimal BrutoTotal { get; set; }
    public decimal CostoTotal { get; set; }
}

public class ContribucionesPatronales
{
    public decimal JubilacionPatronal { get; set; }
    public decimal ObraSocialPatronal { get; set; }
    public decimal Art { get; set; }
    public decimal AsignacionesFamiliares { get; set; }
    public decimal SindicalPatronal { get; set; }

    public static ContribucionesPatronales Desde(Empresa e) => new()
    {
        JubilacionPatronal = e.JubilacionPatronal, ObraSocialPatronal = e.ObraSocialPatronal, Art = e.Art,
        AsignacionesFamiliares = e.AsignacionesFamiliares, SindicalPatronal = e.SindicalPatronal
    };
}

/// <summary>
/// Motor de cálculo de haberes. Es una función pura (sin base de datos ni estado) para poder probarla
/// de forma aislada. Es un port fiel de la lógica del prototipo HTML, con una diferencia deliberada:
/// cada concepto se redondea a 2 decimales, como en un recibo real, de modo que los totales
/// coinciden exactamente con la suma de los renglones impresos.
/// </summary>
public static class CalculoSueldos
{
    public const decimal DivisorHora = 200m;

    public static decimal R(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    /// <summary>Años cumplidos al primer día del período (yyyy-MM). Igual que el prototipo.</summary>
    public static int AniosAntiguedad(DateOnly? fechaIngreso, string? periodo)
    {
        if (fechaIngreso is null) return 0;
        if (!TryParsePeriodo(periodo, out var fin)) return 0;
        var ini = fechaIngreso.Value;
        if (ini > fin) return 0;
        var years = fin.Year - ini.Year;
        var md = fin.Month - ini.Month;
        if (md < 0 || (md == 0 && fin.Day < ini.Day)) years--;
        return Math.Max(0, years);
    }

    public static bool TryParsePeriodo(string? periodo, out DateOnly primerDia)
    {
        primerDia = default;
        if (string.IsNullOrWhiteSpace(periodo) || periodo.Length != 7 || periodo[4] != '-') return false;
        if (!int.TryParse(periodo.AsSpan(0, 4), out var y) || !int.TryParse(periodo.AsSpan(5, 2), out var m)) return false;
        if (y < 1900 || y > 2200 || m < 1 || m > 12) return false;
        primerDia = new DateOnly(y, m, 1);
        return true;
    }

    public static ResultadoCalculo Calcular(
        decimal baseCategoria,
        DateOnly? fechaIngreso,
        string periodo,
        bool afiliadoSindicato,
        ParametrosConvenio p,
        IEnumerable<ConceptoCalc> conceptos,
        NovedadesCalc n)
    {
        var dias = Math.Max(0m, Math.Min(31m, n.Dias));
        var aus = Math.Max(0m, n.Ausencias);
        var diasPago = Math.Max(0m, dias - aus);
        var proporcion = dias >= 30m ? Math.Min(1m, diasPago / 30m) : diasPago / 30m;

        var basico = R(baseCategoria * proporcion);
        var anios = AniosAntiguedad(fechaIngreso, periodo);
        var antiguedad = R(basico * p.Antiguedad * anios);
        var presentismo = R((basico + antiguedad) * p.Presentismo);

        var valorHora = baseCategoria / DivisorHora;
        var mult50 = p.He50 > 0 ? p.He50 : 1.5m;
        var mult100 = p.He100 > 0 ? p.He100 : 2m;
        var he50 = R(valorHora * mult50 * n.He50);
        var he100 = R(valorHora * mult100 * n.He100);

        // Base para "% de haberes": básico + antigüedad + presentismo + horas extra, SIN otros conceptos
        // porcentuales (evita que dos conceptos dependan entre sí o del orden de carga).
        var haberesBase = basico + antiguedad + presentismo + he50 + he100;

        decimal remExtras = 0m, noRemExtras = n.NoRemunerativo, descExtras = 0m;
        var filas = new List<ConceptoLinea>();
        foreach (var con in conceptos)
        {
            var tieneOverride = n.Overrides.TryGetValue(con.Id, out var ov);
            decimal valor;
            if (tieneOverride) valor = R(ov);
            else if (con.Calculo == "pctBasico") valor = R(basico * con.Valor);
            else if (con.Calculo == "pctHaberes") valor = R(haberesBase * con.Valor);
            else valor = R(con.Valor);
            if (valor == 0m) continue;

            if (con.Tipo == "descuento")
            {
                descExtras += valor;
                filas.Add(new ConceptoLinea { Nombre = con.Nombre, Valor = valor, Tipo = "desc", Auto = !tieneOverride });
            }
            else if (con.Remunerativo)
            {
                remExtras += valor;
                filas.Add(new ConceptoLinea { Nombre = con.Nombre, Valor = valor, Tipo = "rem", Auto = !tieneOverride });
            }
            else
            {
                noRemExtras += valor;
                filas.Add(new ConceptoLinea { Nombre = con.Nombre, Valor = valor, Tipo = "noRem", Auto = !tieneOverride });
            }
        }

        var remunerativo = haberesBase + remExtras;
        var jubilacion = R(remunerativo * p.Jubilacion);
        var inssjp = R(remunerativo * p.Inssjp);
        var obraSocial = R(remunerativo * p.ObraSocial);
        var sindicato = afiliadoSindicato ? R(remunerativo * p.Sindicato) : 0m;
        var otros = R(n.OtrosDescuentos);
        var descuentos = jubilacion + inssjp + obraSocial + sindicato + otros + descExtras;
        var noRem = noRemExtras;
        var neto = remunerativo + noRem - descuentos;

        return new ResultadoCalculo
        {
            Basico = basico, Antiguedad = antiguedad, Presentismo = presentismo, He50 = he50, He100 = he100,
            RemExtras = remExtras, Remunerativo = remunerativo, Jubilacion = jubilacion, Inssjp = inssjp,
            ObraSocial = obraSocial, Sindicato = sindicato, Otros = otros, DescExtras = descExtras,
            Descuentos = descuentos, NoRem = noRem, Neto = neto, Anios = anios, ValorHora = R(valorHora),
            ConceptoRows = filas
        };
    }

    /// <summary>Costo laboral total del empleador: bruto + contribuciones patronales (sobre lo remunerativo).</summary>
    public static CostoLaboral CalcularCostoLaboral(ResultadoCalculo c, ContribucionesPatronales cc)
    {
        var b = c.Remunerativo;
        var jub = R(b * cc.JubilacionPatronal);
        var os = R(b * cc.ObraSocialPatronal);
        var art = R(b * cc.Art);
        var asig = R(b * cc.AsignacionesFamiliares);
        var sind = R(b * cc.SindicalPatronal);
        var total = jub + os + art + asig + sind;
        var bruto = c.Remunerativo + c.NoRem;
        return new CostoLaboral
        {
            ContribJub = jub, ContribOS = os, ContribArt = art, ContribAsig = asig, ContribSind = sind,
            ContribTotal = total, BrutoTotal = bruto, CostoTotal = bruto + total
        };
    }

    /// <summary>Días de vacaciones que corresponden por antigüedad según el convenio.</summary>
    public static int DiasVacaciones(Convenio? c, int anios)
    {
        if (c == null) return 0;
        if (anios <= 5) return c.Vac5;
        if (anios <= 10) return c.Vac10;
        return c.VacMas10;
    }

    /// <summary>Valor diario de vacaciones: básico mensual dividido 25 (criterio de la LCT).</summary>
    public static decimal ImporteVacaciones(decimal baseMensual, int dias) => R(baseMensual / 25m * dias);
}
