using LiquidacionSueldos.Web.Domain;
using LiquidacionSueldos.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LiquidacionSueldos.Web.Pages;

public class PruebaVencidaModel(PlanService planes, IConfiguration config) : PageModel
{
    public EstadoPrueba Prueba { get; set; } = EstadoPrueba.NoAplica;
    public string Telefono { get; set; } = "";
    public string Email { get; set; } = "";

    public async Task<IActionResult> OnGetAsync()
    {
        Prueba = await planes.PruebaAsync();
        // Datos de contacto: variables CONTACTO_TELEFONO y CONTACTO_EMAIL en Railway.
        Telefono = config["CONTACTO_TELEFONO"] ?? "";
        Email = config["CONTACTO_EMAIL"] ?? "";
        // Si la prueba sigue vigente (o ya tiene un plan), no hay nada que mostrar acá.
        if (!Prueba.Vencida) return RedirectToPage("/Index");
        return Page();
    }
}
