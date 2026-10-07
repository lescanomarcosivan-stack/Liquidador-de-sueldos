using LiquidacionSueldos.Web.Data;
using LiquidacionSueldos.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace LiquidacionSueldos.Web.Infrastructure;

/// <summary>
/// Las páginas que trabajan sobre los datos de una empresa (empleados, liquidación, recibos, etc.)
/// exigen que haya una empresa activa válida del estudio. Si no, se redirige a elegir/crear una.
/// </summary>
public class EmpresaRequeridaFilter(ITenantContext tenant, AppDbContext db) : IAsyncPageFilter
{
    private static readonly string[] Prefijos =
        ["/Empleados", "/Liquidacion", "/Recibo", "/Vacaciones", "/Notas"];

    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var path = context.ActionDescriptor.ViewEnginePath ?? "";
        var requiere = tenant.IsAuthenticated &&
                       Prefijos.Any(p => path.Equals(p, StringComparison.OrdinalIgnoreCase) ||
                                         path.StartsWith(p + "/", StringComparison.OrdinalIgnoreCase));
        if (requiere)
        {
            var id = tenant.EmpresaId;
            var ok = id.HasValue && await db.Empresas.AnyAsync(e => e.Id == id.Value);
            if (!ok)
            {
                context.Result = new RedirectToPageResult("/SinEmpresa");
                return;
            }
        }
        await next();
    }
}
