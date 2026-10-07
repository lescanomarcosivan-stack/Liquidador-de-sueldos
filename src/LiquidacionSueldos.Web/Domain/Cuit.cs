namespace LiquidacionSueldos.Web.Domain;

/// <summary>Validación de CUIT/CUIL argentino (11 dígitos con dígito verificador módulo 11).</summary>
public static class Cuit
{
    private static readonly int[] Pesos = { 5, 4, 3, 2, 7, 6, 5, 4, 3, 2 };

    /// <summary>Deja solo los dígitos.</summary>
    public static string SoloDigitos(string? valor) =>
        new((valor ?? "").Where(char.IsDigit).ToArray());

    public static bool EsValido(string? valor)
    {
        var d = SoloDigitos(valor);
        if (d.Length != 11) return false;
        var suma = 0;
        for (var i = 0; i < 10; i++) suma += (d[i] - '0') * Pesos[i];
        var verif = 11 - (suma % 11);
        if (verif == 11) verif = 0;
        if (verif == 10) return false;
        return verif == d[10] - '0';
    }

    /// <summary>Formato XX-XXXXXXXX-X. Si no tiene 11 dígitos devuelve el texto original.</summary>
    public static string Formatear(string? valor)
    {
        var d = SoloDigitos(valor);
        return d.Length == 11 ? $"{d[..2]}-{d[2..10]}-{d[10]}" : (valor ?? "").Trim();
    }
}
