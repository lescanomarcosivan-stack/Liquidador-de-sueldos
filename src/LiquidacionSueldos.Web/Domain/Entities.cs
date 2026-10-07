using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace LiquidacionSueldos.Web.Domain;

/// <summary>Roles de la aplicación.</summary>
public static class Roles
{
    public const string Admin = "Administrador";
    public const string Operador = "Operador";
}

/// <summary>Entidad que pertenece a un estudio contable (tenant). Se filtra automáticamente por estudio.</summary>
public interface IEstudioOwned
{
    Guid EstudioId { get; set; }
}

/// <summary>Estudio contable: es el "cliente" del sistema (multi-estudio / SaaS).</summary>
public class Estudio
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [Required, MaxLength(150)] public string Nombre { get; set; } = "";
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}

public class ApplicationUser : IdentityUser
{
    public Guid EstudioId { get; set; }
    [MaxLength(150)] public string NombreCompleto { get; set; } = "";
    public bool Activo { get; set; } = true;
    public Guid? EmpresaActivaId { get; set; }
}

/// <summary>Empresa (empleador) cuyos empleados liquida el estudio.</summary>
public class Empresa : IEstudioOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EstudioId { get; set; }
    [MaxLength(200)] public string RazonSocial { get; set; } = "";
    [MaxLength(20)] public string Cuit { get; set; } = "";
    [MaxLength(250)] public string Domicilio { get; set; } = "";
    [MaxLength(120)] public string Lugar { get; set; } = "";
    [MaxLength(80)] public string Leyenda { get; set; } = "Recibo de haberes";
    /// <summary>Logo como data URL (data:image/png;base64,...).</summary>
    public string? LogoDataUrl { get; set; }

    // Contribuciones patronales (fracciones: 0.19 = 19 %)
    public decimal JubilacionPatronal { get; set; } = 0.19m;
    public decimal ObraSocialPatronal { get; set; } = 0.06m;
    public decimal Art { get; set; } = 0.03m;
    public decimal AsignacionesFamiliares { get; set; } = 0.056m;
    public decimal SindicalPatronal { get; set; } = 0m;
}

/// <summary>Convenio colectivo con sus parámetros de liquidación (compartido por todas las empresas del estudio).</summary>
public class Convenio : IEstudioOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EstudioId { get; set; }
    [MaxLength(200)] public string Nombre { get; set; } = "";

    // Fracciones (0.01 = 1 %). He50/He100 son multiplicadores (1.5 = 150 %).
    public decimal Antiguedad { get; set; } = 0.01m;
    public decimal Presentismo { get; set; } = 0.0833m;
    public decimal He50 { get; set; } = 1.5m;
    public decimal He100 { get; set; } = 2m;
    public decimal Jubilacion { get; set; } = 0.11m;
    public decimal Inssjp { get; set; } = 0.03m;
    public decimal ObraSocial { get; set; } = 0.03m;
    public decimal Sindicato { get; set; } = 0.02m;

    public int Vac5 { get; set; } = 14;
    public int Vac10 { get; set; } = 21;
    public int VacMas10 { get; set; } = 28;

    public decimal? UltimaParitariaPorcentaje { get; set; }
    public DateOnly? UltimaParitariaFecha { get; set; }

    public List<Categoria> Categorias { get; set; } = new();
}

public class Categoria : IEstudioOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EstudioId { get; set; }
    public Guid ConvenioId { get; set; }
    public Convenio? Convenio { get; set; }
    [MaxLength(30)] public string Codigo { get; set; } = "";
    [MaxLength(150)] public string Nombre { get; set; } = "";
    public decimal Basico { get; set; }
}

/// <summary>Concepto adicional configurable (haber o descuento).</summary>
public class Concepto : IEstudioOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EstudioId { get; set; }
    [MaxLength(150)] public string Nombre { get; set; } = "";
    /// <summary>"haber" o "descuento".</summary>
    [MaxLength(20)] public string Tipo { get; set; } = "haber";
    public bool Remunerativo { get; set; } = true;
    /// <summary>"fijo", "pctBasico" o "pctHaberes".</summary>
    [MaxLength(20)] public string Calculo { get; set; } = "fijo";
    /// <summary>Importe (fijo) o fracción (porcentajes).</summary>
    public decimal Valor { get; set; }
}

public class Empleado : IEstudioOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EstudioId { get; set; }
    public Guid EmpresaId { get; set; }
    [MaxLength(30)] public string Legajo { get; set; } = "";
    [MaxLength(100)] public string Apellido { get; set; } = "";
    [MaxLength(100)] public string Nombre { get; set; } = "";
    [MaxLength(15)] public string Dni { get; set; } = "";
    [MaxLength(15)] public string Cuil { get; set; } = "";
    [MaxLength(20)] public string Sexo { get; set; } = "";
    public DateOnly? FechaNacimiento { get; set; }

    public Guid? ConvenioId { get; set; }
    public Convenio? Convenio { get; set; }
    public Guid? CategoriaId { get; set; }
    public Categoria? Categoria { get; set; }

    [MaxLength(150)] public string Puesto { get; set; } = "";
    public DateOnly? FechaIngreso { get; set; }
    public DateOnly? FechaEgreso { get; set; }
    [MaxLength(40)] public string Modalidad { get; set; } = "Tiempo indeterminado";
    [MaxLength(20)] public string Jornada { get; set; } = "Completa";
    public decimal HorasSemanales { get; set; } = 48;

    /// <summary>Básico propio: solo se usa si el empleado NO tiene categoría de convenio asignada.</summary>
    public decimal Basico { get; set; }
    [MaxLength(150)] public string ObraSocial { get; set; } = "";
    public bool AfiliadoSindicato { get; set; } = true;
    [MaxLength(150)] public string Banco { get; set; } = "";
    [MaxLength(1000)] public string Observaciones { get; set; } = "";
    public bool Activo { get; set; } = true;

    public string NombreCompleto => string.IsNullOrWhiteSpace(Nombre) ? Apellido : $"{Apellido}, {Nombre}";

    /// <summary>Básico que realmente se liquida: el de la categoría (si tiene) o, si no, el propio.</summary>
    public decimal BasicoEfectivo => Categoria is { Basico: > 0 } ? Categoria.Basico : Basico;
}

public class Liquidacion : IEstudioOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EstudioId { get; set; }
    public Guid EmpresaId { get; set; }
    /// <summary>Formato yyyy-MM.</summary>
    [MaxLength(7)] public string Periodo { get; set; } = "";
    [MaxLength(30)] public string Tipo { get; set; } = "Mensual";
    public DateOnly FechaPago { get; set; }
    public const string Borrador = "borrador";
    public const string Cerrada = "cerrada";
    [MaxLength(20)] public string Estado { get; set; } = Borrador;
    public int Version { get; set; } = 1;
    public Guid? RectificaDeId { get; set; }
    [MaxLength(150)] public string? CerradaPor { get; set; }
    public DateTime? CerradaFecha { get; set; }
    public List<LiquidacionLinea> Lineas { get; set; } = new();

    public bool EstaCerrada => Estado == Cerrada;
}

public class LiquidacionLinea : IEstudioOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EstudioId { get; set; }
    public Guid LiquidacionId { get; set; }
    public Liquidacion? Liquidacion { get; set; }
    public Guid EmpleadoId { get; set; }
    public Empleado? Empleado { get; set; }
    public bool Seleccionado { get; set; } = true;
    public decimal Dias { get; set; } = 30;
    public decimal Ausencias { get; set; }
    public decimal He50 { get; set; }
    public decimal He100 { get; set; }
    public decimal OtrosDescuentos { get; set; }
    public decimal NoRemunerativo { get; set; }
    /// <summary>JSON: { "idConcepto": importe }. Sobrescribe el valor automático de un concepto para este empleado.</summary>
    public string? ConceptosJson { get; set; }
    /// <summary>JSON con la foto congelada al cerrar la liquidación.</summary>
    public string? SnapshotJson { get; set; }
}

public class Vacacion : IEstudioOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EstudioId { get; set; }
    public Guid EmpresaId { get; set; }
    public Guid EmpleadoId { get; set; }
    public Empleado? Empleado { get; set; }
    [MaxLength(7)] public string Periodo { get; set; } = "";
    public DateOnly Inicio { get; set; }
    public int Dias { get; set; }
    public decimal BaseMensual { get; set; }
    public decimal Importe { get; set; }
    public DateOnly FechaRegistro { get; set; }
}

public class Nota : IEstudioOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EstudioId { get; set; }
    public Guid EmpresaId { get; set; }
    public Guid EmpleadoId { get; set; }
    public Empleado? Empleado { get; set; }
    public DateOnly Fecha { get; set; }
    /// <summary>"vacaciones" o "suspension".</summary>
    [MaxLength(20)] public string Tipo { get; set; } = "vacaciones";
    [MaxLength(7)] public string? Periodo { get; set; }
    public DateOnly? Inicio { get; set; }
    public int Dias { get; set; }
    [MaxLength(1000)] public string? Obs { get; set; }
    public DateOnly? Desde { get; set; }
    public DateOnly? Hasta { get; set; }
    [MaxLength(200)] public string? Articulo { get; set; }
    [MaxLength(1000)] public string? Motivo { get; set; }
}

public class AuditoriaEntry : IEstudioOwned
{
    public long Id { get; set; }
    public Guid EstudioId { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    [MaxLength(150)] public string Usuario { get; set; } = "";
    [MaxLength(60)] public string Accion { get; set; } = "";
    [MaxLength(1000)] public string Detalle { get; set; } = "";
}
