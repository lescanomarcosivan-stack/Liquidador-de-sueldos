namespace LiquidacionSueldos.Web.Domain;

/// <summary>
/// Foto congelada de un recibo al cerrar la liquidación: valores calculados, parámetros del convenio,
/// novedades, datos del empleado y del empleador. Un recibo cerrado nunca se recalcula, aunque después
/// cambien el convenio, el sueldo o los datos de la empresa.
/// </summary>
public class SnapshotLinea
{
    public ResultadoCalculo Calculo { get; set; } = new();
    public string ConvenioNombre { get; set; } = "";
    public string CategoriaNombre { get; set; } = "";
    public string CategoriaCodigo { get; set; } = "";
    public decimal BasicoAplicado { get; set; }
    public bool AfiliadoSindicato { get; set; } = true;
    public ParametrosConvenio Parametros { get; set; } = new();
    public NovedadesCalc Novedades { get; set; } = new();
    public SnapshotEmpleado Empleado { get; set; } = new();
    public SnapshotEmpleador Empleador { get; set; } = new();
    public ContribucionesPatronales Contribuciones { get; set; } = new();
    public string Fecha { get; set; } = "";
}

public class SnapshotEmpleado
{
    public string Nombre { get; set; } = "";
    public string Legajo { get; set; } = "";
    public string Cuil { get; set; } = "";
    public string Dni { get; set; } = "";
    /// <summary>yyyy-MM-dd o vacío.</summary>
    public string FechaIngreso { get; set; } = "";
}

public class SnapshotEmpleador
{
    public string RazonSocial { get; set; } = "";
    public string Cuit { get; set; } = "";
    public string Domicilio { get; set; } = "";
    public string Lugar { get; set; } = "";
    public string Leyenda { get; set; } = "Recibo de haberes";
    public string? LogoDataUrl { get; set; }
}
