using System.Security.Claims;

namespace LiquidacionSueldos.Web.Services;

/// <summary>
/// El "superadministrador de la plataforma" es el dueño del servicio (quien cobra los planes), no un usuario de un estudio.
/// Se define por la variable de entorno SUPERADMIN_EMAIL; solo esa cuenta ve y usa la página /Plataforma.
/// </summary>
public static class SuperAdmin
{
    public static bool EsSuperAdmin(ClaimsPrincipal user, IConfiguration config)
    {
        var autorizado = config["SUPERADMIN_EMAIL"];
        if (string.IsNullOrWhiteSpace(autorizado) || user.Identity?.IsAuthenticated != true) return false;
        var email = user.FindFirstValue(ClaimTypes.Email);
        return string.Equals(email?.Trim(), autorizado.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
