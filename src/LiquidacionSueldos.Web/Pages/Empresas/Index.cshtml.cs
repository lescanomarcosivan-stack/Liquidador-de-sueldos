using System.ComponentModel.DataAnnotations;
using LiquidacionSueldos.Web.Data;
using LiquidacionSueldos.Web.Domain;
using LiquidacionSueldos.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LiquidacionSueldos.Web.Pages.Empresas;

public class IndexModel(AppDbContext db, ITenantContext tenant, IAuditoria audit, EmpresaActivaService activa) : PageModel
{
    public class EmpresaInput
    {
        public Guid? Id { get; set; }
        [Required(ErrorMessage = "Ingresá la razón social.")] public string RazonSocial { get; set; } = "";
        [Required(ErrorMessage = "Ingresá el CUIT.")] public string Cuit { get; set; } = "";
        public string? Domicilio { get; set; }
        public string? Lugar { get; set; }
        public string? Leyenda { get; set; } = "Recibo de haberes";
        // En porcentaje (19 = 19 %)
        [Display(Name = "Jubilación patronal"), Range(0, 100, ErrorMessage = "{0} debe estar entre {1} y {2} %.")]
        public decimal JubilacionPatronal { get; set; } = 19m;
        [Display(Name = "Obra social patronal"), Range(0, 100, ErrorMessage = "{0} debe estar entre {1} y {2} %.")]
        public decimal ObraSocialPatronal { get; set; } = 6m;
        [Display(Name = "ART"), Range(0, 100, ErrorMessage = "{0} debe estar entre {1} y {2} %.")]
        public decimal Art { get; set; } = 3m;
        [Display(Name = "Asignaciones familiares"), Range(0, 100, ErrorMessage = "{0} debe estar entre {1} y {2} %.")]
        public decimal AsignacionesFamiliares { get; set; } = 5.6m;
        [Display(Name = "Aporte sindical patronal"), Range(0, 100, ErrorMessage = "{0} debe estar entre {1} y {2} %.")]
        public decimal SindicalPatronal { get; set; }
    }

    public List<Empresa> Lista { get; set; } = new();
    [BindProperty] public EmpresaInput Input { get; set; } = new();
    [BindProperty] public IFormFile? Logo { get; set; }
    [BindProperty] public bool QuitarLogo { get; set; }
    public string? LogoActual { get; set; }
    public bool Editando => Input.Id.HasValue;
    [TempData] public string? Ok { get; set; }
    [TempData] public string? Error { get; set; }

    private static decimal Pct(decimal v) => Math.Clamp(v, 0m, 100m) / 100m;

    public async Task OnGetAsync(Guid? id)
    {
        await CargarAsync();
        if (id.HasValue && await db.Empresas.FirstOrDefaultAsync(e => e.Id == id.Value) is { } e)
        {
            Input = new EmpresaInput
            {
                Id = e.Id, RazonSocial = e.RazonSocial, Cuit = e.Cuit, Domicilio = e.Domicilio, Lugar = e.Lugar,
                Leyenda = e.Leyenda, JubilacionPatronal = Fmt.Limpio(e.JubilacionPatronal * 100m),
                ObraSocialPatronal = Fmt.Limpio(e.ObraSocialPatronal * 100m), Art = Fmt.Limpio(e.Art * 100m),
                AsignacionesFamiliares = Fmt.Limpio(e.AsignacionesFamiliares * 100m), SindicalPatronal = Fmt.Limpio(e.SindicalPatronal * 100m)
            };
            LogoActual = e.LogoDataUrl;
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!tenant.IsAdmin) return Forbid();
        if (!ModelState.IsValid) { await CargarAsync(); return Page(); }
        if (!Domain.Cuit.EsValido(Input.Cuit))
        {
            ModelState.AddModelError("", "El CUIT no es válido (revisá el dígito verificador).");
            await CargarAsync(); return Page();
        }

        string? logo = null;
        if (Logo is { Length: > 0 })
        {
            if (Logo.Length > 300 * 1024 || (Logo.ContentType != "image/png" && Logo.ContentType != "image/jpeg"))
            {
                ModelState.AddModelError("", "El logo debe ser PNG o JPG de hasta 300 KB.");
                await CargarAsync(); return Page();
            }
            using var ms = new MemoryStream();
            await Logo.CopyToAsync(ms);
            logo = $"data:{Logo.ContentType};base64,{Convert.ToBase64String(ms.ToArray())}";
        }

        var cuit = Domain.Cuit.Formatear(Input.Cuit);
        var idActual = Input.Id ?? Guid.Empty;
        var repetida = await db.Empresas.Where(x => x.Cuit == cuit && x.Id != idActual).Select(x => x.RazonSocial).FirstOrDefaultAsync();
        if (repetida != null)
        {
            ModelState.AddModelError("", $"Ya existe una empresa con el CUIT {cuit} en este estudio ({repetida}).");
            await CargarAsync(); return Page();
        }
        Empresa e;
        var nueva = !Input.Id.HasValue;
        if (nueva)
        {
            e = new Empresa { EstudioId = tenant.EstudioId };
            db.Empresas.Add(e);
        }
        else
        {
            var existente = await db.Empresas.FirstOrDefaultAsync(x => x.Id == Input.Id!.Value);
            if (existente == null) return NotFound();
            e = existente;
        }

        e.RazonSocial = Input.RazonSocial.Trim();
        e.Cuit = cuit;
        e.Domicilio = Input.Domicilio?.Trim() ?? "";
        e.Lugar = Input.Lugar?.Trim() ?? "";
        e.Leyenda = string.IsNullOrWhiteSpace(Input.Leyenda) ? "Recibo de haberes" : Input.Leyenda.Trim();
        e.JubilacionPatronal = Pct(Input.JubilacionPatronal);
        e.ObraSocialPatronal = Pct(Input.ObraSocialPatronal);
        e.Art = Pct(Input.Art);
        e.AsignacionesFamiliares = Pct(Input.AsignacionesFamiliares);
        e.SindicalPatronal = Pct(Input.SindicalPatronal);
        if (logo != null) e.LogoDataUrl = logo;
        else if (QuitarLogo) e.LogoDataUrl = null;

        audit.Registrar(nueva ? "empresa_creada" : "empresa_editada",
            $"{(nueva ? "Creó" : "Editó")} la empresa {e.RazonSocial} (CUIT {e.Cuit})");
        await db.SaveChangesAsync();

        if (nueva && tenant.EmpresaId == null) await activa.EstablecerAsync(e.Id);
        Ok = nueva ? "Empresa creada." : "Cambios guardados.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEliminarAsync(Guid id)
    {
        if (!tenant.IsAdmin) return Forbid();
        var e = await db.Empresas.FirstOrDefaultAsync(x => x.Id == id);
        if (e == null) return NotFound();

        var tieneDatos = await db.Empleados.AnyAsync(x => x.EmpresaId == id) ||
                         await db.Liquidaciones.AnyAsync(x => x.EmpresaId == id);
        if (tieneDatos)
        {
            Error = "No se puede eliminar: la empresa tiene empleados o liquidaciones.";
            return RedirectToPage();
        }
        db.Empresas.Remove(e);
        audit.Registrar("empresa_eliminada", $"Eliminó la empresa {e.RazonSocial}");
        await db.SaveChangesAsync();

        if (tenant.EmpresaId == id) await activa.EstablecerAsync(null);
        Ok = "Empresa eliminada.";
        return RedirectToPage();
    }

    private async Task CargarAsync() => Lista = await db.Empresas.OrderBy(e => e.RazonSocial).ToListAsync();
}
