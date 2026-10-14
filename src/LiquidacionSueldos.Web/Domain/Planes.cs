namespace LiquidacionSueldos.Web.Domain;

public enum Recurso { Empresas, Empleados, Usuarios }

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
        new(Prueba, "Prueba", MaxEmpresas: 1, MaxEmpleados: 10, MaxUsuarios: 2),
        new(Basico, "Básico", MaxEmpresas: 3, MaxEmpleados: 50, MaxUsuarios: 3),
        new(Estudio, "Estudio", MaxEmpresas: 15, MaxEmpleados: 400, MaxUsuarios: 10),
        new(Ilimitado, "Ilimitado", int.MaxValue, int.MaxValue, int.MaxValue),
    ];

    /// <summary>Plan por código; un código desconocido cae en el más restrictivo (Prueba).</summary>
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
}
