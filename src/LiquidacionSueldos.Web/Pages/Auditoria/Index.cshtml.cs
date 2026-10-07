using LiquidacionSueldos.Web.Data;
using LiquidacionSueldos.Web.Domain;
using LiquidacionSueldos.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LiquidacionSueldos.Web.Pages.Auditoria;

public class IndexModel(AppDbContext db, ITenantContext tenant) : PageModel
{
    public const int PorPagina = 100;

    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public int Pagina { get; set; } = 1;
    public List<AuditoriaEntry> Items { get; set; } = new();
    public int Total { get; set; }
    public int Paginas => Math.Max(1, (int)Math.Ceiling(Total / (double)PorPagina));

    public async Task<IActionResult> OnGetAsync()
    {
        if (!tenant.IsAdmin) return Forbid();
        var q = db.Auditorias.AsQueryable();
        if (!string.IsNullOrWhiteSpace(Q))
        {
            var t = Q.Trim().ToLower();
            q = q.Where(a => a.Usuario.ToLower().Contains(t) || a.Accion.ToLower().Contains(t) || a.Detalle.ToLower().Contains(t));
        }
        Total = await q.CountAsync();
        Pagina = Math.Clamp(Pagina, 1, Paginas);
        Items = await q.OrderByDescending(a => a.Fecha).ThenByDescending(a => a.Id)
            .Skip((Pagina - 1) * PorPagina).Take(PorPagina).ToListAsync();
        return Page();
    }
}
