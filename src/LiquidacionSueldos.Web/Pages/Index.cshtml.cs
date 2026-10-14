using LiquidacionSueldos.Web.Data;
using LiquidacionSueldos.Web.Domain;
using LiquidacionSueldos.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LiquidacionSueldos.Web.Pages;

public class IndexModel(AppDbContext db, ITenantContext tenant, PlanService planes) : PageModel
{
    public Empresa? Empresa { get; set; }
    public int Empleados { get; set; }
    public int Convenios { get; set; }
    public int Cerradas { get; set; }
    public int Borradores { get; set; }
    public UsoPlan? Plan { get; set; }

    public async Task OnGetAsync()
    {
        Plan = await planes.UsoAsync();
        var id = tenant.EmpresaId;
        if (id == null) return;
        Empresa = await db.Empresas.FirstOrDefaultAsync(e => e.Id == id.Value);
        if (Empresa == null) return;
        Empleados = await db.Empleados.CountAsync(e => e.EmpresaId == Empresa.Id && e.Activo);
        Convenios = await db.Convenios.CountAsync();
        Cerradas = await db.Liquidaciones.CountAsync(l => l.EmpresaId == Empresa.Id && l.Estado == Domain.Liquidacion.Cerrada);
        Borradores = await db.Liquidaciones.CountAsync(l => l.EmpresaId == Empresa.Id && l.Estado == Domain.Liquidacion.Borrador);
    }
}
