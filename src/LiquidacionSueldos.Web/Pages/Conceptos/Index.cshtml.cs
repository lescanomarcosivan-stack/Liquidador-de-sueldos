using System.ComponentModel.DataAnnotations;
using LiquidacionSueldos.Web.Data;
using LiquidacionSueldos.Web.Domain;
using LiquidacionSueldos.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LiquidacionSueldos.Web.Pages.Conceptos;

public class IndexModel(AppDbContext db, ITenantContext tenant, IAuditoria audit) : PageModel
{
    public class ConceptoInput
    {
        public Guid? Id { get; set; }
        [Required(ErrorMessage = "Ingresá el nombre.")] public string Nombre { get; set; } = "";
        public string Tipo { get; set; } = "haber";
        public bool Remunerativo { get; set; } = true;
        public string Calculo { get; set; } = "fijo";
        /// <summary>true = no se aplica solo: se agrega empleado por empleado en la liquidación.</summary>
        public bool Manual { get; set; }
        /// <summary>Importe ($) si es fijo; porcentaje (5 = 5 %) en los demás casos.</summary>
        public decimal Valor { get; set; }
    }

    public List<Concepto> Lista { get; set; } = new();
    [BindProperty] public ConceptoInput Input { get; set; } = new();
    [TempData] public string? Ok { get; set; }
    [TempData] public string? Error { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid? id)
    {
        if (!tenant.IsAdmin) return Forbid();
        Lista = await db.Conceptos.OrderBy(c => c.Nombre).ToListAsync();
        var c = id.HasValue ? Lista.FirstOrDefault(x => x.Id == id.Value) : null;
        if (c != null)
            Input = new ConceptoInput
            {
                Id = c.Id, Nombre = c.Nombre, Tipo = c.Tipo, Remunerativo = c.Remunerativo, Calculo = c.Calculo, Manual = c.Manual,
                Valor = Fmt.Limpio(c.Calculo == "fijo" ? c.Valor : c.Valor * 100m)
            };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!tenant.IsAdmin) return Forbid();
        if (!ModelState.IsValid) { Error = "Ingresá el nombre del concepto."; return RedirectToPage(); }

        var tipo = Input.Tipo == "descuento" ? "descuento" : "haber";
        var calculo = Input.Calculo is "pctBasico" or "pctHaberes" or "pctRemunerativo" or "pctNoRemunerativo" or "pctBruto" ? Input.Calculo : "fijo";
        if (tipo == "haber" && calculo is "pctRemunerativo" or "pctNoRemunerativo" or "pctBruto")
        {
            Error = "No se guardó: calcular sobre el total remunerativo, no remunerativo o el bruto solo es posible en descuentos (un haber no puede depender de sí mismo).";
            return RedirectToPage();
        }
        var valor = Math.Max(0m, Input.Valor);
        if (calculo != "fijo") valor = Math.Min(valor, 1000m) / 100m;

        Concepto c;
        var nuevo = !Input.Id.HasValue;
        if (nuevo) { c = new Concepto { EstudioId = tenant.EstudioId }; db.Conceptos.Add(c); }
        else
        {
            var e = await db.Conceptos.FirstOrDefaultAsync(x => x.Id == Input.Id!.Value);
            if (e == null) return NotFound();
            c = e;
        }
        c.Nombre = Input.Nombre.Trim();
        c.Tipo = tipo;
        c.Remunerativo = tipo == "haber" && Input.Remunerativo;
        c.Calculo = calculo;
        c.Manual = Input.Manual;
        c.Valor = Math.Round(valor, 6);

        audit.Registrar(nuevo ? "concepto_creado" : "concepto_editado", $"{(nuevo ? "Creó" : "Editó")} el concepto {c.Nombre}");
        await db.SaveChangesAsync();
        Ok = nuevo ? "Concepto creado." : "Concepto guardado.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEliminarAsync(Guid id)
    {
        if (!tenant.IsAdmin) return Forbid();
        var c = await db.Conceptos.FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound();
        db.Conceptos.Remove(c);
        audit.Registrar("concepto_eliminado", $"Eliminó el concepto {c.Nombre}");
        await db.SaveChangesAsync();
        Ok = "Concepto eliminado.";
        return RedirectToPage();
    }
}
