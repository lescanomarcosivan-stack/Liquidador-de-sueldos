using System.Text.Json;
using LiquidacionSueldos.Web.Data;
using LiquidacionSueldos.Web.Domain;
using Microsoft.EntityFrameworkCore;

namespace LiquidacionSueldos.Web.Services;

/// <summary>Novedades que carga el operador para un empleado en una liquidación.</summary>
public class LineaInput
{
    public Guid EmpleadoId { get; set; }
    public bool Seleccionado { get; set; }
    public decimal Dias { get; set; } = 30;
    public decimal Ausencias { get; set; }
    public decimal He50 { get; set; }
    public decimal He100 { get; set; }
    public decimal OtrosDescuentos { get; set; }
    public decimal NoRemunerativo { get; set; }
}

/// <summary>Reglas de negocio de la liquidación: borrador, cierre con foto congelada y rectificativas.</summary>
public class LiquidacionService(AppDbContext db, ITenantContext tenant, IAuditoria audit)
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    // ---------------- Utilidades de cálculo ----------------

    public async Task<List<ConceptoCalc>> ConceptosAsync()
    {
        var lista = await db.Conceptos.OrderBy(c => c.Nombre).ToListAsync();
        return lista.Select(c => new ConceptoCalc
        {
            Id = c.Id.ToString(), Nombre = c.Nombre, Tipo = c.Tipo, Remunerativo = c.Remunerativo,
            Calculo = c.Calculo, Valor = c.Valor
        }).ToList();
    }

    public static NovedadesCalc Novedades(LiquidacionLinea l)
    {
        var n = new NovedadesCalc
        {
            Dias = l.Dias, Ausencias = l.Ausencias, He50 = l.He50, He100 = l.He100,
            OtrosDescuentos = l.OtrosDescuentos, NoRemunerativo = l.NoRemunerativo
        };
        if (!string.IsNullOrWhiteSpace(l.ConceptosJson))
        {
            try { n.Overrides = JsonSerializer.Deserialize<Dictionary<string, decimal>>(l.ConceptosJson, Json) ?? new(); }
            catch (JsonException) { n.Overrides = new(); }
        }
        return n;
    }

    /// <summary>Cálculo en vivo con los datos actuales (el empleado debe traer Convenio y Categoria cargados).</summary>
    public static ResultadoCalculo CalcularLive(Empleado e, LiquidacionLinea l, string periodo, IEnumerable<ConceptoCalc> conceptos) =>
        CalculoSueldos.Calcular(e.BasicoEfectivo, e.FechaIngreso, periodo, e.AfiliadoSindicato,
            ParametrosConvenio.Desde(e.Convenio), conceptos, Novedades(l));

    public static SnapshotLinea? LeerSnapshot(LiquidacionLinea l)
    {
        if (string.IsNullOrWhiteSpace(l.SnapshotJson)) return null;
        try { return JsonSerializer.Deserialize<SnapshotLinea>(l.SnapshotJson, Json); }
        catch (JsonException) { return null; }
    }

    /// <summary>Si la línea tiene foto congelada, devuelve esos valores; si no, calcula en vivo.</summary>
    public static ResultadoCalculo Resultado(Liquidacion liq, LiquidacionLinea l, IEnumerable<ConceptoCalc> conceptos)
    {
        var snap = LeerSnapshot(l);
        if (snap != null) return snap.Calculo;
        return CalcularLive(l.Empleado!, l, liq.Periodo, conceptos);
    }

    // ---------------- Consultas ----------------

    public IQueryable<Liquidacion> ConLineas() =>
        db.Liquidaciones
            .Include(x => x.Lineas).ThenInclude(l => l.Empleado).ThenInclude(e => e!.Convenio)
            .Include(x => x.Lineas).ThenInclude(l => l.Empleado).ThenInclude(e => e!.Categoria)
            .AsSplitQuery();

    /// <summary>Última versión (original o rectificativa más nueva) de un período/tipo de la empresa activa.</summary>
    public Task<Liquidacion?> BuscarVigenteAsync(string periodo, string tipo)
    {
        var empresaId = tenant.RequireEmpresaId;
        return ConLineas()
            .Where(x => x.EmpresaId == empresaId && x.Periodo == periodo && x.Tipo == tipo)
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync();
    }

    public async Task<List<Empleado>> EmpleadosActivosAsync()
    {
        var empresaId = tenant.RequireEmpresaId;
        return await db.Empleados.Include(e => e.Convenio).Include(e => e.Categoria)
            .Where(e => e.EmpresaId == empresaId && e.Activo)
            .OrderBy(e => e.Apellido).ThenBy(e => e.Nombre)
            .ToListAsync();
    }

    // ---------------- Borrador ----------------

    public async Task<(Liquidacion? Liquidacion, string? Error)> GuardarBorradorAsync(
        string periodo, string tipo, DateOnly fechaPago, IEnumerable<LineaInput> inputs)
    {
        if (!CalculoSueldos.TryParsePeriodo(periodo, out _))
            return (null, "El período no es válido.");

        var empresaId = tenant.RequireEmpresaId;
        var liq = await BuscarVigenteAsync(periodo, tipo);
        if (liq is { EstaCerrada: true })
            return (null, "La liquidación está cerrada y no se puede modificar. Creá una rectificativa para corregirla.");

        var idsValidos = (await db.Empleados.Where(e => e.EmpresaId == empresaId).Select(e => e.Id).ToListAsync()).ToHashSet();

        var esNueva = liq == null;
        if (liq == null)
        {
            liq = new Liquidacion
            {
                EstudioId = tenant.EstudioId, EmpresaId = empresaId, Periodo = periodo, Tipo = tipo,
                Estado = Liquidacion.Borrador, Version = 1
            };
            db.Liquidaciones.Add(liq);
        }
        liq.FechaPago = fechaPago;

        foreach (var inp in inputs)
        {
            if (!idsValidos.Contains(inp.EmpleadoId)) continue;
            var linea = liq.Lineas.FirstOrDefault(l => l.EmpleadoId == inp.EmpleadoId);
            if (linea == null)
            {
                linea = new LiquidacionLinea { EstudioId = tenant.EstudioId, LiquidacionId = liq.Id, EmpleadoId = inp.EmpleadoId };
                db.LiquidacionLineas.Add(linea);
                liq.Lineas.Add(linea);
            }
            linea.Seleccionado = inp.Seleccionado;
            linea.Dias = Math.Clamp(inp.Dias, 0m, 31m);
            linea.Ausencias = Math.Max(0m, inp.Ausencias);
            linea.He50 = Math.Max(0m, inp.He50);
            linea.He100 = Math.Max(0m, inp.He100);
            linea.OtrosDescuentos = Math.Max(0m, inp.OtrosDescuentos);
            linea.NoRemunerativo = Math.Max(0m, inp.NoRemunerativo);
        }

        audit.Registrar("liquidacion_guardada",
            $"{(esNueva ? "Creó" : "Actualizó")} el borrador de liquidación {periodo} · {tipo}");
        await db.SaveChangesAsync();

        // Recargar con empleados/convenios para que el llamador pueda mostrar totales.
        return (await BuscarVigenteAsync(periodo, tipo), null);
    }

    // ---------------- Cierre ----------------

    public async Task<string?> CerrarAsync(Liquidacion liq)
    {
        if (liq.EstaCerrada) return "La liquidación ya está cerrada.";

        var seleccionadas = liq.Lineas.Where(l => l.Seleccionado).ToList();
        if (seleccionadas.Count == 0) return "Seleccioná al menos un empleado para cerrar la liquidación.";

        var sinBasico = seleccionadas
            .Where(l => l.Empleado == null || l.Empleado.BasicoEfectivo <= 0)
            .Select(l => l.Empleado?.NombreCompleto ?? "(empleado)")
            .ToList();
        if (sinBasico.Count > 0)
            return "No se puede cerrar: estos empleados no tienen sueldo básico (asignales una categoría o un básico): "
                   + string.Join("; ", sinBasico);

        var empresa = await db.Empresas.FirstOrDefaultAsync(e => e.Id == liq.EmpresaId);
        if (empresa == null) return "No se encontró la empresa de la liquidación.";

        var conceptos = await ConceptosAsync();
        var empleador = new SnapshotEmpleador
        {
            RazonSocial = empresa.RazonSocial, Cuit = empresa.Cuit, Domicilio = empresa.Domicilio,
            Lugar = empresa.Lugar, Leyenda = string.IsNullOrWhiteSpace(empresa.Leyenda) ? "Recibo de haberes" : empresa.Leyenda,
            LogoDataUrl = empresa.LogoDataUrl
        };
        var contrib = ContribucionesPatronales.Desde(empresa);
        var hoy = Fmt.FechaIso(Fmt.HoyAr());

        foreach (var l in liq.Lineas)
        {
            if (!l.Seleccionado || l.Empleado == null) { l.SnapshotJson = null; continue; }
            var e = l.Empleado;
            var calc = CalcularLive(e, l, liq.Periodo, conceptos);
            var snap = new SnapshotLinea
            {
                Calculo = calc,
                ConvenioNombre = e.Convenio?.Nombre ?? "Sin convenio",
                CategoriaNombre = e.Categoria?.Nombre ?? "",
                CategoriaCodigo = e.Categoria?.Codigo ?? "",
                BasicoAplicado = e.BasicoEfectivo,
                AfiliadoSindicato = e.AfiliadoSindicato,
                Parametros = ParametrosConvenio.Desde(e.Convenio),
                Novedades = Novedades(l),
                Empleado = new SnapshotEmpleado
                {
                    Nombre = e.NombreCompleto, Legajo = e.Legajo, Cuil = e.Cuil, Dni = e.Dni,
                    FechaIngreso = Fmt.FechaIso(e.FechaIngreso)
                },
                Empleador = empleador,
                Contribuciones = contrib,
                Fecha = hoy
            };
            l.SnapshotJson = JsonSerializer.Serialize(snap, Json);
        }

        liq.Estado = Liquidacion.Cerrada;
        liq.CerradaPor = tenant.DisplayName;
        liq.CerradaFecha = DateTime.UtcNow;

        audit.Registrar("liquidacion_cerrada",
            $"Cerró la liquidación {liq.Periodo} · {liq.Tipo}{(liq.Version > 1 ? $" · v{liq.Version} (rectificativa)" : "")} — {seleccionadas.Count} empleados");
        await db.SaveChangesAsync();
        return null;
    }

    // ---------------- Rectificativa ----------------

    public async Task<(Liquidacion? Nueva, string? Error)> CrearRectificativaAsync(Guid liquidacionId)
    {
        var empresaId = tenant.RequireEmpresaId;
        var orig = await ConLineas().FirstOrDefaultAsync(x => x.Id == liquidacionId && x.EmpresaId == empresaId);
        if (orig == null) return (null, "No se encontró la liquidación.");
        if (!orig.EstaCerrada) return (null, "Solo se puede rectificar una liquidación cerrada.");

        var haySuperior = await db.Liquidaciones.AnyAsync(x =>
            x.EmpresaId == empresaId && x.Periodo == orig.Periodo && x.Tipo == orig.Tipo && x.Version > orig.Version);
        if (haySuperior) return (null, "Ya existe una versión más nueva de esta liquidación.");

        var nueva = new Liquidacion
        {
            EstudioId = tenant.EstudioId, EmpresaId = empresaId, Periodo = orig.Periodo, Tipo = orig.Tipo,
            FechaPago = orig.FechaPago, Estado = Liquidacion.Borrador, Version = orig.Version + 1, RectificaDeId = orig.Id
        };
        foreach (var l in orig.Lineas)
        {
            nueva.Lineas.Add(new LiquidacionLinea
            {
                EstudioId = tenant.EstudioId, EmpleadoId = l.EmpleadoId, Seleccionado = l.Seleccionado,
                Dias = l.Dias, Ausencias = l.Ausencias, He50 = l.He50, He100 = l.He100,
                OtrosDescuentos = l.OtrosDescuentos, NoRemunerativo = l.NoRemunerativo, ConceptosJson = l.ConceptosJson
            });
        }
        db.Liquidaciones.Add(nueva);
        audit.Registrar("liquidacion_rectificativa",
            $"Creó la rectificativa v{nueva.Version} de {nueva.Periodo} · {nueva.Tipo} (a partir de v{orig.Version})");
        await db.SaveChangesAsync();
        return (await BuscarVigenteAsync(nueva.Periodo, nueva.Tipo), null);
    }
}
