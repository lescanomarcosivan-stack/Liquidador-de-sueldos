using LiquidacionSueldos.Web.Data;
using LiquidacionSueldos.Web.Domain;
using LiquidacionSueldos.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LiquidacionSueldos.Web.Pages;

public class SinEmpresaModel(AppDbContext db, EmpresaActivaService activa) : PageModel
{
    public List<Empresa> Empresas { get; set; } = new();

    public async Task OnGetAsync() => Empresas = await db.Empresas.OrderBy(e => e.RazonSocial).ToListAsync();

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        await activa.EstablecerAsync(id);
        return RedirectToPage("/Index");
    }
}
