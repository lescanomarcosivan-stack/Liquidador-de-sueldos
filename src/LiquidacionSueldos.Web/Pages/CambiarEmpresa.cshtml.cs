using LiquidacionSueldos.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LiquidacionSueldos.Web.Pages;

public class CambiarEmpresaModel(EmpresaActivaService activa) : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/Index");

    public async Task<IActionResult> OnPostAsync(Guid? id)
    {
        if (id.HasValue) await activa.EstablecerAsync(id);
        return RedirectToPage("/Index");
    }
}
