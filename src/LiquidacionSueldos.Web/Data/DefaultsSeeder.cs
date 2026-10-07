using LiquidacionSueldos.Web.Domain;

namespace LiquidacionSueldos.Web.Data;

/// <summary>Datos iniciales que recibe cada estudio nuevo (los mismos que traía el prototipo HTML).</summary>
public static class DefaultsSeeder
{
    public static void Sembrar(AppDbContext db, Guid estudioId)
    {
        db.Convenios.AddRange(
            Conv(estudioId, "Comercio (CCT 130/75)", 0.01m, 0.02m),
            Conv(estudioId, "Gastronómicos (CCT 389/04)", 0.01m, 0.02m),
            Conv(estudioId, "Camioneros (CCT 40/89)", 0.015m, 0.025m));

        db.Conceptos.AddRange(
            new Concepto { EstudioId = estudioId, Nombre = "Adicional por título", Tipo = "haber", Remunerativo = true, Calculo = "fijo", Valor = 0 },
            new Concepto { EstudioId = estudioId, Nombre = "Bono no remunerativo", Tipo = "haber", Remunerativo = false, Calculo = "fijo", Valor = 0 },
            new Concepto { EstudioId = estudioId, Nombre = "Descuento por préstamo", Tipo = "descuento", Remunerativo = false, Calculo = "fijo", Valor = 0 });
    }

    private static Convenio Conv(Guid estudioId, string nombre, decimal antiguedad, decimal sindicato) => new()
    {
        EstudioId = estudioId,
        Nombre = nombre,
        Antiguedad = antiguedad,
        Presentismo = 0.0833m,
        He50 = 1.5m,
        He100 = 2m,
        Jubilacion = 0.11m,
        Inssjp = 0.03m,
        ObraSocial = 0.03m,
        Sindicato = sindicato,
        Vac5 = 14,
        Vac10 = 21,
        VacMas10 = 28
    };
}
