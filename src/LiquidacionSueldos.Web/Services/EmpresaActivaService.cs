using LiquidacionSueldos.Web.Data;
using LiquidacionSueldos.Web.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LiquidacionSueldos.Web.Services;

/// <summary>Cambia la empresa activa del usuario y refresca su sesión para que las claims se actualicen.</summary>
public class EmpresaActivaService(
    AppDbContext db, UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn,
    ITenantContext tenant, IHttpContextAccessor http)
{
    public async Task<bool> EstablecerAsync(Guid? empresaId)
    {
        if (empresaId.HasValue && !await db.Empresas.AnyAsync(e => e.Id == empresaId.Value))
            return false; // no existe o es de otro estudio

        var user = await users.GetUserAsync(http.HttpContext!.User);
        if (user == null) return false;
        user.EmpresaActivaId = empresaId;
        var res = await users.UpdateAsync(user);
        if (!res.Succeeded) return false;

        tenant.SetEmpresa(empresaId);
        await signIn.RefreshSignInAsync(user);
        return true;
    }
}
