namespace LiquidacionSueldos.Web.Domain;

public enum Recurso { Empresas, Empleados, Usuarios }

/// <summary>Situación del período de prueba de un estudio.</summary>
public record EstadoPrueba(bool EnPrueba, DateTime? Vence, int DiasRestantes, bool Vencida)
{
    public static readonly EstadoPrueba NoAplica = new(false, null, 0, false);
}

/// <summary>Un plan comercial con sus topes (int.MaxValue = sin límite).</summary>
public record PlanInfo(string Codigo, string Nombre, int MaxEmpresas, int MaxEmpleados, int MaxUsuarios)
{
    public int Max(Recurso r) => r switch
    {
        Recurso.Empresas => MaxEmpresas,
        Recurso.Empleados => MaxEmpleados,
        _ => MaxUsuarios
    };
}

/// <summary>
/// Planes del servicio. Los topes viven en código (no en la base): para cambiar un precio o un límite
/// alcanza con editar esta lista. Cada estudio guarda solo el código de su plan.
/// </summary>
public static class Planes
{
    public const string Prueba = "Prueba";
    public const string Basico = "Basico";
    public const string Estudio = "Estudio";
    public const string Ilimitado = "Ilimitado";

    public static readonly PlanInfo[] Todos =
    [
        // Prueba: acceso completo durante 30 días (el límite es el tiempo, no la cantidad). Ver CalcularPrueba.
        new(Prueba, "Prueba", int.MaxValue, int.MaxValue, int.MaxValue),
        new(Basico, "Básico", MaxEmpresas: 3, MaxEmpleados: 50, MaxUsuarios: 3),
        new(Estudio, "Estudio", MaxEmpresas: 15, MaxEmpleados: 400, MaxUsuarios: 10),
        new(Ilimitado, "Ilimitado", int.MaxValue, int.MaxValue, int.MaxValue),
    ];

    /// <summary>Duración del período de prueba gratuito.</summary>
    public const int DiasPrueba = 30;

    /// <summary>Plan por código; un código desconocido cae en Prueba (que vence a los 30 días).</summary>
    public static PlanInfo Obtener(string? codigo) =>
        Todos.FirstOrDefault(p => string.Equals(p.Codigo, codigo, StringComparison.OrdinalIgnoreCase)) ?? Todos[0];

    public static bool Existe(string? codigo) =>
        Todos.Any(p => string.Equals(p.Codigo, codigo, StringComparison.OrdinalIgnoreCase));

    /// <summary>true si, con <paramref name="actual"/> ya cargados, agregar uno más supera el tope del plan.</summary>
    public static bool AlcanzoElLimite(PlanInfo plan, Recurso recurso, int actual) => actual >= plan.Max(recurso);

    public static string Etiqueta(int max) => max == int.MaxValue ? "sin límite" : max.ToString();

    public static string MensajeLimite(PlanInfo plan, Recurso recurso)
    {
        var que = recurso switch { Recurso.Empresas => "empresas", Recurso.Empleados => "empleados activos", _ => "usuarios activos" };
        return $"Tu plan {plan.Nombre} permite hasta {plan.Max(recurso)} {que}. Para agregar más, pasá a un plan superior.";
    }

    /// <summary>
    /// Estado de la prueba gratuita. Solo el plan Prueba tiene vencimiento: <paramref name="pruebaVenceUtc"/> si está cargado,
    /// o el alta + 30 días. Cualquier otro plan devuelve <see cref="EstadoPrueba.NoAplica"/>.
    /// </summary>
    public static EstadoPrueba CalcularPrueba(string? codigoPlan, DateTime creadoEnUtc, DateTime? pruebaVenceUtc, DateTime ahoraUtc)
    {
        if (Obtener(codigoPlan).Codigo != Prueba) return EstadoPrueba.NoAplica;
        var vence = pruebaVenceUtc ?? creadoEnUtc.AddDays(DiasPrueba);
        var resta = vence - ahoraUtc;
        if (resta <= TimeSpan.Zero) return new EstadoPrueba(true, vence, 0, true);
        return new EstadoPrueba(true, vence, (int)Math.Ceiling(resta.TotalDays), false);
    }

    public const string MensajePruebaVencida = "Tu período de prueba de 30 días terminó. Tus datos siguen guardados y podés consultarlos, pero para seguir cargando y liquidando hace falta contratar un plan.";
}
