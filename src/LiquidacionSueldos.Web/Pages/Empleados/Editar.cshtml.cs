using System.ComponentModel.DataAnnotations;
using LiquidacionSueldos.Web.Data;
using LiquidacionSueldos.Web.Domain;
using LiquidacionSueldos.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LiquidacionSueldos.Web.Pages.Empleados;

public class EditarModel(AppDbContext db, ITenantContext tenant, IAuditoria audit) : PageModel
{
    public class EmpleadoInput
    {
        public Guid? Id { get; set; }
        [Required(ErrorMessage = "Ingresá el legajo.")] public string Legajo { get; set; } = "";
        [Required(ErrorMessage = "Ingresá el apellido.")] public string Apellido { get; set; } = "";
        [Required(ErrorMessage = "Ingresá el nombre.")] public string Nombre { get; set; } = "";
        public string? Dni { get; set; }
        public string? Cuil { get; set; }
        public string? Sexo { get; set; }
        public DateOnly? FechaNacimiento { get; set; }
        public Guid? ConvenioId { get; set; }
        /// <summary>Id de categoría, vacío (sin categoría) o "__new__" (crear una nueva).</summary>
        public string? CategoriaSel { get; set; }
        public string? NuevaCatCodigo { get; set; }
        public string? NuevaCatNombre { get; set; }
        public decimal NuevaCatBasico { get; set; }
        public string? Puesto { get; set; }
        public DateOnly? FechaIngreso { get; set; }
        public DateOnly? FechaEgreso { get; set; }
        public string? Modalidad { get; set; } = "Tiempo indeterminado";
        public string? Jornada { get; set; } = "Completa";
        public decimal HorasSemanales { get; set; } = 48;
        public decimal Basico { get; set; }
        public string? ObraSocial { get; set; }
        public string? Banco { get; set; }
        public bool AfiliadoSindicato { get; set; } = true;
        public bool Activo { get; set; } = true;
        public string? Observaciones { get; set; }
    }

    [BindProperty] public EmpleadoInput Input { get; set; } = new();
    public List<Convenio> Convenios { get; set; } = new();
    public List<Categoria> Categorias { get; set; } = new();
    public bool EsNuevo => !Input.Id.HasValue;
    public bool EsAdmin => tenant.IsAdmin;

    private async Task CargarListasAsync()
    {
        Convenios = await db.Convenios.OrderBy(c => c.Nombre).ToListAsync();
        Categorias = await db.Categorias.OrderBy(c => c.Codigo).ThenBy(c => c.Nombre).ToListAsync();
    }

    public async Task<IActionResult> OnGetAsync(Guid? id)
    {
        await CargarListasAsync();
        if (id.HasValue)
        {
            var empresaId = tenant.RequireEmpresaId;
            var e = await db.Empleados.FirstOrDefaultAsync(x => x.Id == id.Value && x.EmpresaId == empresaId);
            if (e == null) return NotFound();
            Input = new EmpleadoInput
            {
                Id = e.Id, Legajo = e.Legajo, Apellido = e.Apellido, Nombre = e.Nombre, Dni = e.Dni, Cuil = e.Cuil,
                Sexo = e.Sexo, FechaNacimiento = e.FechaNacimiento, ConvenioId = e.ConvenioId,
                CategoriaSel = e.CategoriaId?.ToString(), Puesto = e.Puesto, FechaIngreso = e.FechaIngreso,
                FechaEgreso = e.FechaEgreso, Modalidad = e.Modalidad, Jornada = e.Jornada,
                HorasSemanales = Fmt.Limpio(e.HorasSemanales), Basico = Fmt.Limpio(e.Basico), ObraSocial = e.ObraSocial, Banco = e.Banco,
                AfiliadoSindicato = e.AfiliadoSindicato, Activo = e.Activo, Observaciones = e.Observaciones
            };
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var empresaId = tenant.RequireEmpresaId;
        await CargarListasAsync();
        var legajo = Input.Legajo?.Trim() ?? "";

        if (!string.IsNullOrWhiteSpace(Input.Cuil) && !Cuit.EsValido(Input.Cuil))
            ModelState.AddModelError("", "El CUIL no es válido (revisá el dígito verificador).");
        if (!string.IsNullOrEmpty(Input.CategoriaSel) && Input.CategoriaSel != "__new__" && !Guid.TryParse(Input.CategoriaSel, out _))
            ModelState.AddModelError("", "La categoría seleccionada no es válida.");

        Guid? categoriaId = null;
        Convenio? convenio = null;
        if (Input.ConvenioId.HasValue)
        {
            convenio = Convenios.FirstOrDefault(c => c.Id == Input.ConvenioId.Value);
            if (convenio == null) ModelState.AddModelError("", "El convenio seleccionado no existe.");
        }

        if (Input.CategoriaSel == "__new__")
        {
            if (!EsAdmin) ModelState.AddModelError("", "Solo un Administrador puede crear categorías.");
            else if (convenio == null) ModelState.AddModelError("", "Elegí un convenio para crear la categoría.");
            else if (string.IsNullOrWhiteSpace(Input.NuevaCatNombre)) ModelState.AddModelError("", "Ingresá el nombre de la nueva categoría.");
            else if (Input.NuevaCatBasico <= 0) ModelState.AddModelError("", "Ingresá el sueldo básico de la nueva categoría.");
        }
        else if (Guid.TryParse(Input.CategoriaSel, out var cid))
        {
            var cat = Categorias.FirstOrDefault(c => c.Id == cid);
            if (cat == null || convenio == null || cat.ConvenioId != convenio.Id)
                ModelState.AddModelError("", "La categoría no pertenece al convenio elegido.");
            else categoriaId = cid;
        }

        var existeLegajo = await db.Empleados.AnyAsync(e =>
            e.EmpresaId == empresaId && e.Legajo == legajo && e.Id != (Input.Id ?? Guid.Empty));
        if (existeLegajo) ModelState.AddModelError("", $"Ya existe un empleado con el legajo {legajo} en esta empresa.");

        if (!ModelState.IsValid) return Page();

        Empleado e;
        var nuevo = !Input.Id.HasValue;
        if (nuevo) { e = new Empleado { EstudioId = tenant.EstudioId, EmpresaId = empresaId }; db.Empleados.Add(e); }
        else
        {
            var ex = await db.Empleados.FirstOrDefaultAsync(x => x.Id == Input.Id!.Value && x.EmpresaId == empresaId);
            if (ex == null) return NotFound();
            e = ex;
        }

        if (Input.CategoriaSel == "__new__")
        {
            var cat = new Categoria
            {
                EstudioId = tenant.EstudioId, ConvenioId = convenio!.Id,
                Codigo = (Input.NuevaCatCodigo ?? "").Trim(), Nombre = Input.NuevaCatNombre!.Trim(),
                Basico = CalculoSueldos.R(Input.NuevaCatBasico)
            };
            db.Categorias.Add(cat);
            categoriaId = cat.Id;
            audit.Registrar("categoria_creada", $"Creó la categoría {cat.Nombre} en el convenio {convenio.Nombre} (alta de empleado)");
        }

        e.Legajo = legajo;
        e.Apellido = Input.Apellido.Trim();
        e.Nombre = Input.Nombre.Trim();
        e.Dni = Input.Dni?.Trim() ?? "";
        e.Cuil = string.IsNullOrWhiteSpace(Input.Cuil) ? "" : Cuit.Formatear(Input.Cuil);
        e.Sexo = Input.Sexo ?? "";
        e.FechaNacimiento = Input.FechaNacimiento;
        e.ConvenioId = convenio?.Id;
        e.CategoriaId = categoriaId;
        e.Puesto = Input.Puesto?.Trim() ?? "";
        e.FechaIngreso = Input.FechaIngreso;
        e.FechaEgreso = Input.FechaEgreso;
        e.Modalidad = Input.Modalidad?.Trim() ?? "";
        e.Jornada = Input.Jornada ?? "Completa";
        e.HorasSemanales = Math.Clamp(Input.HorasSemanales, 0m, 60m);
        // El básico sale de la categoría; el propio solo aplica sin categoría.
        e.Basico = categoriaId.HasValue ? 0m : CalculoSueldos.R(Math.Max(0m, Input.Basico));
        e.ObraSocial = Input.ObraSocial?.Trim() ?? "";
        e.Banco = Input.Banco?.Trim() ?? "";
        e.AfiliadoSindicato = Input.AfiliadoSindicato;
        e.Activo = Input.Activo;
        e.Observaciones = Input.Observaciones?.Trim() ?? "";

        audit.Registrar(nuevo ? "empleado_creado" : "empleado_editado",
            $"{(nuevo ? "Creó" : "Editó")} al empleado {e.NombreCompleto} (legajo {e.Legajo})");
        await db.SaveChangesAsync();
        TempData["Ok"] = nuevo ? "Empleado creado." : "Cambios guardados.";
        return RedirectToPage("Index");
    }
}
