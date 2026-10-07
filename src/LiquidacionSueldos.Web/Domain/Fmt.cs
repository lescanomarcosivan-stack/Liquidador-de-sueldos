using System.Globalization;

namespace LiquidacionSueldos.Web.Domain;

/// <summary>Formatos de presentación en español de Argentina, sin depender de la cultura del servidor.</summary>
public static class Fmt
{
    private static readonly NumberFormatInfo Ar = new()
    {
        NumberDecimalSeparator = ",",
        NumberGroupSeparator = ".",
        NumberGroupSizes = new[] { 3 },
        NegativeSign = "-"
    };

    private static readonly string[] Meses =
    {
        "enero", "febrero", "marzo", "abril", "mayo", "junio",
        "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre"
    };

    public static string MesNombre(int mes) => Meses[Math.Clamp(mes, 1, 12) - 1];

    /// <summary>$ 1.234,56</summary>
    public static string Money(decimal v) => "$ " + v.ToString("N2", Ar);

    /// <summary>1.234,56 (sin símbolo)</summary>
    public static string Num2(decimal v) => v.ToString("N2", Ar);

    /// <summary>Quita ceros finales de un decimal (8.330000 → 8.33) para mostrarlo en campos de formulario.</summary>
    public static decimal Limpio(decimal v) => v / 1.0000000000000000000000000000m;

    /// <summary>Número para un input type=number (punto decimal, sin separador de miles).</summary>
    public static string Input(decimal v) => v.ToString("0.##########", CultureInfo.InvariantCulture);

    /// <summary>Fracción mostrada como porcentaje para un input (0.0833 → 8.33).</summary>
    public static string PctInput(decimal fraccion) => (fraccion * 100m).ToString("0.######", CultureInfo.InvariantCulture);

    /// <summary>Fracción mostrada como porcentaje legible (0.11 → 11,00%).</summary>
    public static string Pct(decimal fraccion) => (fraccion * 100m).ToString("0.00", Ar) + "%";

    public static string Fecha(DateOnly? d) => d is null ? "—" : d.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    /// <summary>Argentina no tiene horario de verano: UTC-3 fijo (evita depender de tzdata en el contenedor).</summary>
    public static DateTime AhoraAr() => DateTime.UtcNow.AddHours(-3);
    public static DateOnly HoyAr() => DateOnly.FromDateTime(AhoraAr());

    public static string FechaHora(DateTime utc) =>
        utc.AddHours(-3).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    public static string FechaIso(DateOnly? d) => d is null ? "" : d.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>5 de octubre de 2026</summary>
    public static string FechaLarga(DateOnly? d) =>
        d is null ? "—" : $"{d.Value.Day} de {Meses[d.Value.Month - 1]} de {d.Value.Year}";

    public static string PeriodoActual() => AhoraAr().ToString("yyyy-MM", CultureInfo.InvariantCulture);

    /// <summary>2026-08 → agosto 2026</summary>
    public static string PeriodoLargo(string? periodo)
    {
        if (!CalculoSueldos.TryParsePeriodo(periodo, out var d)) return periodo ?? "";
        return $"{Meses[d.Month - 1]} {d.Year}";
    }
}
