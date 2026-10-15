using LiquidacionSueldos.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LiquidacionSueldos.Web.Infrastructure;

/// <summary>
/// Cuando vence la prueba gratuita el sistema queda en modo solo lectura: se puede entrar, consultar e imprimir,
/// pero cualquier envío de formulario que guarde datos se redirige a la pantalla "Prueba vencida".
/// Quedan libres: iniciar/cerrar sesión, cambiar de empresa, el recálculo en vivo (no guarda nada) y la página
/// de Plataforma del dueño del servicio.
/// </summary>
public class PruebaVencidaFilter(ITenantContext tenant, PlanService planes, IConfiguration config) : IAsyncPageFilter
{
    private static readonly string[] Libres = ["/Account", "/CambiarEmpresa", "/PruebaVencida", "/Plataforma", "/Error"];

    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var req = context.HttpContext.Request;
        var escribe = tenant.IsAuthenticated && !HttpMethods.IsGet(req.Method) && !HttpMethods.IsHead(req.Method);
        if (escribe)
        {
            var path = context.ActionDescriptor.ViewEnginePath ?? "";
            var libre = Libres.Any(p => path.Equals(p, StringComparison.OrdinalIgnoreCase) ||
                                        path.StartsWith(p + "/", StringComparison.OrdinalIgnoreCase)) ||
                        context.HandlerMethod?.Name == "Calcular" ||
                        SuperAdmin.EsSuperAdmin(context.HttpContext.User, config);
            if (!libre && (await planes.PruebaAsync()).Vencida)
            {
                context.Result = new RedirectToPageResult("/PruebaVencida");
                return;
            }
        }
        await next();
    }
}
