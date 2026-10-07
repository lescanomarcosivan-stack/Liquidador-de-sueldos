using LiquidacionSueldos.Web.Domain;
using Xunit;

namespace LiquidacionSueldos.Tests;

/// <summary>
/// Los valores esperados se calcularon aparte, aplicando las mismas fórmulas del prototipo HTML
/// (antigüedad = básico × % × años; presentismo = (básico + antigüedad) × %; HE = básico/200 × multiplicador × horas;
/// aportes sobre el total remunerativo), redondeando cada concepto a 2 decimales.
/// </summary>
public class CalculoSueldosTests
{
    private static ParametrosConvenio Comercio() => new()
    {
        Antiguedad = 0.01m, Presentismo = 0.0833m, He50 = 1.5m, He100 = 2m,
        Jubilacion = 0.11m, Inssjp = 0.03m, ObraSocial = 0.03m, Sindicato = 0.02m
    };

    [Fact]
    public void Caso_basico_con_antiguedad_y_presentismo()
    {
        var r = CalculoSueldos.Calcular(1_000_000m, new DateOnly(2020, 3, 15), "2026-09", true,
            Comercio(), Array.Empty<ConceptoCalc>(), new NovedadesCalc());

        Assert.Equal(6, r.Anios);
        Assert.Equal(1_000_000.00m, r.Basico);
        Assert.Equal(60_000.00m, r.Antiguedad);
        Assert.Equal(88_298.00m, r.Presentismo);
        Assert.Equal(1_148_298.00m, r.Remunerativo);
        Assert.Equal(126_312.78m, r.Jubilacion);
        Assert.Equal(34_448.94m, r.Inssjp);
        Assert.Equal(34_448.94m, r.ObraSocial);
        Assert.Equal(22_965.96m, r.Sindicato);
        Assert.Equal(218_176.62m, r.Descuentos);
        Assert.Equal(930_121.38m, r.Neto);
    }

    [Fact]
    public void Caso_completo_con_ausencias_horas_extra_y_conceptos()
    {
        var p = Comercio();
        p.Antiguedad = 0.015m;
        p.Sindicato = 0.025m;
        var conceptos = new[]
        {
            new ConceptoCalc { Id = "a", Nombre = "Adicional", Tipo = "haber", Remunerativo = true, Calculo = "pctHaberes", Valor = 0.05m },
            new ConceptoCalc { Id = "b", Nombre = "Préstamo", Tipo = "descuento", Remunerativo = false, Calculo = "fijo", Valor = 20_000m },
            new ConceptoCalc { Id = "c", Nombre = "Bono", Tipo = "haber", Remunerativo = false, Calculo = "fijo", Valor = 30_000m }
        };
        var n = new NovedadesCalc { Dias = 30, Ausencias = 3, He50 = 10, He100 = 4, OtrosDescuentos = 15_000m, NoRemunerativo = 50_000m };

        var r = CalculoSueldos.Calcular(850_000m, new DateOnly(2023, 1, 10), "2026-09", afiliadoSindicato: false, p, conceptos, n);

        Assert.Equal(3, r.Anios);
        Assert.Equal(765_000.00m, r.Basico);
        Assert.Equal(34_425.00m, r.Antiguedad);
        Assert.Equal(66_592.10m, r.Presentismo);
        Assert.Equal(63_750.00m, r.He50);
        Assert.Equal(34_000.00m, r.He100);
        Assert.Equal(1_011_955.46m, r.Remunerativo);
        Assert.Equal(111_315.10m, r.Jubilacion);
        Assert.Equal(30_358.66m, r.Inssjp);
        Assert.Equal(30_358.66m, r.ObraSocial);
        Assert.Equal(0m, r.Sindicato);
        Assert.Equal(80_000.00m, r.NoRem);
        Assert.Equal(207_032.42m, r.Descuentos);
        Assert.Equal(884_923.04m, r.Neto);
    }

    [Fact]
    public void Quincena_paga_proporcional_a_los_dias()
    {
        var r = CalculoSueldos.Calcular(600_000m, null, "2026-09", true,
            Comercio(), Array.Empty<ConceptoCalc>(), new NovedadesCalc { Dias = 15 });

        Assert.Equal(300_000.00m, r.Basico);
        Assert.Equal(24_990.00m, r.Presentismo);
        Assert.Equal(324_990.00m, r.Remunerativo);
        Assert.Equal(263_241.90m, r.Neto);
    }

    [Fact]
    public void Override_manual_reemplaza_el_valor_automatico_del_concepto()
    {
        var conceptos = new[]
        {
            new ConceptoCalc { Id = "x", Nombre = "Bono", Tipo = "haber", Remunerativo = false, Calculo = "fijo", Valor = 1_000m }
        };
        var n = new NovedadesCalc { Overrides = new() { ["x"] = 5_000m } };

        var r = CalculoSueldos.Calcular(100_000m, null, "2026-09", true, Comercio(), conceptos, n);

        Assert.Equal(5_000m, r.NoRem);
        Assert.Single(r.ConceptoRows);
        Assert.False(r.ConceptoRows[0].Auto);
    }

    [Fact]
    public void Concepto_en_cero_no_aparece()
    {
        var conceptos = new[]
        {
            new ConceptoCalc { Id = "x", Nombre = "Bono", Tipo = "haber", Remunerativo = true, Calculo = "fijo", Valor = 0m }
        };
        var r = CalculoSueldos.Calcular(100_000m, null, "2026-09", true, Comercio(), conceptos, new NovedadesCalc());
        Assert.Empty(r.ConceptoRows);
    }

    [Theory]
    [InlineData("2020-09-02", "2026-09", 5)]   // cumple años después del 1º del período
    [InlineData("2020-09-01", "2026-09", 6)]   // cumple justo el 1º
    [InlineData("2020-03-15", "2026-09", 6)]
    [InlineData("2026-10-01", "2026-09", 0)]   // ingresa después del período
    public void Anios_de_antiguedad(string ingreso, string periodo, int esperado)
    {
        Assert.Equal(esperado, CalculoSueldos.AniosAntiguedad(DateOnly.Parse(ingreso), periodo));
    }

    [Fact]
    public void Costo_laboral_suma_contribuciones_sobre_lo_remunerativo()
    {
        var r = new ResultadoCalculo { Remunerativo = 1_000_000m, NoRem = 100_000m };
        var cc = new ContribucionesPatronales
        {
            JubilacionPatronal = 0.19m, ObraSocialPatronal = 0.06m, Art = 0.03m, AsignacionesFamiliares = 0.056m, SindicalPatronal = 0m
        };

        var k = CalculoSueldos.CalcularCostoLaboral(r, cc);

        Assert.Equal(190_000m, k.ContribJub);
        Assert.Equal(60_000m, k.ContribOS);
        Assert.Equal(30_000m, k.ContribArt);
        Assert.Equal(56_000m, k.ContribAsig);
        Assert.Equal(336_000m, k.ContribTotal);
        Assert.Equal(1_100_000m, k.BrutoTotal);
        Assert.Equal(1_436_000m, k.CostoTotal);
    }

    [Theory]
    [InlineData(3, 14)]
    [InlineData(5, 14)]
    [InlineData(6, 21)]
    [InlineData(10, 21)]
    [InlineData(11, 28)]
    public void Dias_de_vacaciones_segun_antiguedad(int anios, int esperado)
    {
        var c = new Convenio { Vac5 = 14, Vac10 = 21, VacMas10 = 28 };
        Assert.Equal(esperado, CalculoSueldos.DiasVacaciones(c, anios));
    }

    [Fact]
    public void Importe_de_vacaciones_es_basico_sobre_25_por_dias()
    {
        Assert.Equal(560_000.00m, CalculoSueldos.ImporteVacaciones(1_000_000m, 14));
    }
}
