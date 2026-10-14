using System.ComponentModel.DataAnnotations;
using LiquidacionSueldos.Web.Data;
using LiquidacionSueldos.Web.Domain;
using LiquidacionSueldos.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LiquidacionSueldos.Web.Pages.Convenios;

public class IndexModel(AppDbContext db, ITenantContext tenant, IAuditoria audit) : PageModel
{
    public class ConvenioInput
    {
        public Guid? Id { get; set; }
        [Required(ErrorMessage = "Ingresá el nombre del convenio.")] public string Nombre { get; set; } = "";
        // Todos los porcentajes se cargan como % (1 = 1 %, 150 = 150 %)
        public decimal Antiguedad { get; set; } = 1m;
        public decimal Presentismo { get; set; } = 8.33m;
        public decimal He50 { get; set; } = 150m;
        public decimal He100 { get; set; } = 200m;
        public decimal Jubilacion { get; set; } = 11m;
        public decimal Inssjp { get; set; } = 3m;
        public decimal ObraSocial { get; set; } = 3m;
        public decimal Sindicato { get; set; } = 2m;
        public int Vac5 { get; set; } = 14;
        public int Vac10 { get; set; } = 21;
        public int VacMas10 { get; set; } = 28;
    }

    public List<Convenio> Lista { get; set; } = new();
    public Convenio? Actual { get; set; }
    public bool EsNuevo { get; set; }
    [BindProperty] public ConvenioInput Input { get; set; } = new();
    [TempData] public string? Ok { get; set; }
    [TempData] public string? Error { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid? id, bool nuevo = false)
    {
        if (!tenant.IsAdmin) return Forbid();
        Lista = await db.Convenios.OrderBy(c => c.Nombre).ToListAsync();
        EsNuevo = nuevo;
        if (!nuevo)
        {
            var sel = id ?? Lista.FirstOrDefault()?.Id;
            if (sel != null)
            {
                Actual = await db.Convenios.Include(c => c.Categorias).FirstOrDefaultAsync(c => c.Id == sel.Value);
                if (Actual != null) Input = Desde(Actual);
            }
        }
        return Page();
    }

    private static ConvenioInput Desde(Convenio c) => new()
    {
        Id = c.Id, Nombre = c.Nombre, Antiguedad = Fmt.Limpio(c.Antiguedad * 100m), Presentismo = Fmt.Limpio(c.Presentismo * 100m),
        He50 = Fmt.Limpio(c.He50 * 100m), He100 = Fmt.Limpio(c.He100 * 100m), Jubilacion = Fmt.Limpio(c.Jubilacion * 100m), Inssjp = Fmt.Limpio(c.Inssjp * 100m),
        ObraSocial = Fmt.Limpio(c.ObraSocial * 100m), Sindicato = Fmt.Limpio(c.Sindicato * 100m),
        Vac5 = c.Vac5, Vac10 = c.Vac10, VacMas10 = c.VacMas10
    };

    public async Task<IActionResult> OnPostGuardarAsync()
    {
        if (!tenant.IsAdmin) return Forbid();
        if (!ModelState.IsValid) { Error = "Ingresá el nombre del convenio."; return RedirectToPage(new { id = Input.Id, nuevo = !Input.Id.HasValue }); }

        // Los parámetros fuera de rango se rechazan (antes se recortaban en silencio).
        var fuera = new List<string>();
        void Rango(string nombre, decimal valor, decimal min, decimal max)
        {
            if (valor < min || valor > max) fuera.Add($"{nombre} debe estar entre {min} y {max}");
        }
        Rango("Antigüedad (% por año)", Input.Antiguedad, 0, 100);
        Rango("Presentismo (%)", Input.Presentismo, 0, 100);
        Rango("Hora extra 50% (multiplicador %)", Input.He50, 100, 1000);
        Rango("Hora extra 100% (multiplicador %)", Input.He100, 100, 1000);
        Rango("Jubilación (%)", Input.Jubilacion, 0, 100);
        Rango("INSSJP (%)", Input.Inssjp, 0, 100);
        Rango("Obra social (%)", Input.ObraSocial, 0, 100);
        Rango("Cuota sindical (%)", Input.Sindicato, 0, 100);
        Rango("Vacaciones hasta 5 años (días)", Input.Vac5, 0, 60);
        Rango("Vacaciones 5 a 10 años (días)", Input.Vac10, 0, 60);
        Rango("Vacaciones más de 10 años (días)", Input.VacMas10, 0, 60);
        if (Input.Jubilacion + Input.Inssjp + Input.ObraSocial + Input.Sindicato > 100)
            fuera.Add("la suma de los aportes del trabajador supera el 100 %");
        if (fuera.Count > 0)
        {
            Error = "No se guardó: " + string.Join("; ", fuera) + ".";
            return RedirectToPage(new { id = Input.Id, nuevo = !Input.Id.HasValue });
        }

        Convenio c;
        var nuevo = !Input.Id.HasValue;
        if (nuevo) { c = new Convenio { EstudioId = tenant.EstudioId }; db.Convenios.Add(c); }
        else
        {
            var e = await db.Convenios.FirstOrDefaultAsync(x => x.Id == Input.Id!.Value);
            if (e == null) return NotFound();
            c = e;
        }

        c.Nombre = Input.Nombre.Trim();
        c.Antiguedad = P(Input.Antiguedad);
        c.Presentismo = P(Input.Presentismo);
        c.He50 = P(Input.He50);
        c.He100 = P(Input.He100);
        c.Jubilacion = P(Input.Jubilacion);
        c.Inssjp = P(Input.Inssjp);
        c.ObraSocial = P(Input.ObraSocial);
        c.Sindicato = P(Input.Sindicato);
        c.Vac5 = Math.Clamp(Input.Vac5, 0, 60);
        c.Vac10 = Math.Clamp(Input.Vac10, 0, 60);
        c.VacMas10 = Math.Clamp(Input.VacMas10, 0, 60);

        audit.Registrar(nuevo ? "convenio_creado" : "convenio_editado", $"{(nuevo ? "Creó" : "Editó")} el convenio {c.Nombre}");
        await db.SaveChangesAsync();
        Ok = nuevo ? "Convenio creado." : "Parámetros guardados.";
        return RedirectToPage(new { id = c.Id });
    }

    private static decimal P(decimal pct) => Math.Clamp(pct, 0m, 1000m) / 100m;

    public async Task<IActionResult> OnPostEliminarAsync(Guid id)
    {
        if (!tenant.IsAdmin) return Forbid();
        var c = await db.Convenios.FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound();
        if (await db.Empleados.AnyAsync(e => e.ConvenioId == id))
        {
            Error = "No se puede eliminar: hay empleados asignados a este convenio.";
            return RedirectToPage(new { id });
        }
        db.Convenios.Remove(c);
        audit.Registrar("convenio_eliminado", $"Eliminó el convenio {c.Nombre}");
        await db.SaveChangesAsync();
        Ok = "Convenio eliminado.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAgregarCategoriaAsync(Guid id, string? codigo, string nombre, decimal basico)
    {
        if (!tenant.IsAdmin) return Forbid();
        var c = await db.Convenios.FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound();
        if (string.IsNullOrWhiteSpace(nombre)) { Error = "Ingresá el nombre de la categoría."; return RedirectToPage(new { id }); }
        if (basico <= 0) { Error = "El sueldo básico de la categoría debe ser mayor a cero."; return RedirectToPage(new { id }); }

        db.Categorias.Add(new Categoria
        {
            EstudioId = tenant.EstudioId, ConvenioId = id, Codigo = (codigo ?? "").Trim(),
            Nombre = nombre.Trim(), Basico = CalculoSueldos.R(Math.Max(0, basico))
        });
        audit.Registrar("categoria_creada", $"Agregó la categoría {nombre.Trim()} al convenio {c.Nombre}");
        await db.SaveChangesAsync();
        Ok = "Categoría agregada.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostGuardarCategoriaAsync(Guid id, Guid catId, string? codigo, string nombre, decimal basico)
    {
        if (!tenant.IsAdmin) return Forbid();
        var cat = await db.Categorias.FirstOrDefaultAsync(x => x.Id == catId && x.ConvenioId == id);
        if (cat == null) return NotFound();
        if (string.IsNullOrWhiteSpace(nombre)) { Error = "El nombre de la categoría no puede quedar vacío."; return RedirectToPage(new { id }); }
        if (basico <= 0) { Error = "El sueldo básico de la categoría debe ser mayor a cero."; return RedirectToPage(new { id }); }

        var anterior = cat.Basico;
        cat.Codigo = (codigo ?? "").Trim();
        cat.Nombre = nombre.Trim();
        cat.Basico = CalculoSueldos.R(Math.Max(0, basico));
        audit.Registrar("categoria_editada",
            $"Editó la categoría {cat.Nombre} (básico {Fmt.Money(anterior)} → {Fmt.Money(cat.Basico)})");
        await db.SaveChangesAsync();
        Ok = "Categoría guardada.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostEliminarCategoriaAsync(Guid id, Guid catId)
    {
        if (!tenant.IsAdmin) return Forbid();
        var cat = await db.Categorias.FirstOrDefaultAsync(x => x.Id == catId && x.ConvenioId == id);
        if (cat == null) return NotFound();
        if (await db.Empleados.AnyAsync(e => e.CategoriaId == catId))
        {
            Error = "No se puede eliminar: hay empleados con esta categoría.";
            return RedirectToPage(new { id });
        }
        db.Categorias.Remove(cat);
        audit.Registrar("categoria_eliminada", $"Eliminó la categoría {cat.Nombre}");
        await db.SaveChangesAsync();
        Ok = "Categoría eliminada.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostParitariaAsync(Guid id, decimal porcentaje)
    {
        if (!tenant.IsAdmin) return Forbid();
        var c = await db.Convenios.Include(x => x.Categorias).FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound();
        if (porcentaje is < -50 or > 500 || porcentaje == 0)
        {
            Error = "Ingresá un porcentaje distinto de cero (entre -50 y 500).";
            return RedirectToPage(new { id });
        }
        if (c.Categorias.Count == 0) { Error = "El convenio no tiene categorías."; return RedirectToPage(new { id }); }

        var factor = 1m + porcentaje / 100m;
        foreach (var cat in c.Categorias) cat.Basico = CalculoSueldos.R(cat.Basico * factor);
        c.UltimaParitariaPorcentaje = porcentaje;
        c.UltimaParitariaFecha = Fmt.HoyAr();

        audit.Registrar("paritaria_aplicada",
            $"Aplicó paritaria de {Fmt.Num2(porcentaje)}% al convenio {c.Nombre} ({c.Categorias.Count} categorías)");
        await db.SaveChangesAsync();
        Ok = $"Paritaria aplicada: {Fmt.Num2(porcentaje)}% sobre {c.Categorias.Count} categorías.";
        return RedirectToPage(new { id });
    }
}
