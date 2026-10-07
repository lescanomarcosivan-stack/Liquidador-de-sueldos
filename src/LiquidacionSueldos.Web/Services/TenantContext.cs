using System.Security.Claims;
using LiquidacionSueldos.Web.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace LiquidacionSueldos.Web.Services;

/// <summary>Datos del usuario autenticado: su estudio, su empresa activa y su rol.</summary>
public interface ITenantContext
{
    bool IsAuthenticated { get; }
    Guid EstudioId { get; }
    Guid? EmpresaId { get; }
    /// <summary>Empresa activa; lanza excepción si no hay (el filtro global ya redirige antes).</summary>
    Guid RequireEmpresaId { get; }
    string UserId { get; }
    string DisplayName { get; }
    bool IsAdmin { get; }
    /// <summary>Fuerza la empresa activa durante la solicitud actual (las claims se actualizan en la próxima).</summary>
    void SetEmpresa(Guid? empresaId);
}

public class TenantContext(IHttpContextAccessor http) : ITenantContext
{
    private Guid? _override;
    private bool _overrideSet;

    private ClaimsPrincipal? Principal => http.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid EstudioId =>
        Guid.TryParse(Principal?.FindFirstValue(AppClaims.EstudioId), out var g) ? g : Guid.Empty;

    public Guid? EmpresaId
    {
        get
        {
            if (_overrideSet) return _override;
            return Guid.TryParse(Principal?.FindFirstValue(AppClaims.EmpresaId), out var g) ? g : null;
        }
    }

    public Guid RequireEmpresaId =>
        EmpresaId ?? throw new InvalidOperationException("No hay una empresa activa seleccionada.");

    public string UserId => Principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

    public string DisplayName =>
        Principal?.FindFirstValue(AppClaims.Nombre) is { Length: > 0 } n ? n : Principal?.Identity?.Name ?? "Sistema";

    public bool IsAdmin => Principal?.IsInRole(Roles.Admin) == true;

    public void SetEmpresa(Guid? empresaId)
    {
        _override = empresaId;
        _overrideSet = true;
    }
}

public static class AppClaims
{
    public const string EstudioId = "estudio_id";
    public const string EmpresaId = "empresa_id";
    public const string Nombre = "nombre_completo";
}

/// <summary>Agrega al usuario autenticado las claims de estudio, empresa activa y nombre.</summary>
public class AppClaimsFactory(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>(userManager, roleManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim(AppClaims.EstudioId, user.EstudioId.ToString()));
        identity.AddClaim(new Claim(AppClaims.Nombre, string.IsNullOrWhiteSpace(user.NombreCompleto) ? (user.UserName ?? "") : user.NombreCompleto));
        if (user.EmpresaActivaId.HasValue)
            identity.AddClaim(new Claim(AppClaims.EmpresaId, user.EmpresaActivaId.Value.ToString()));
        return identity;
    }
}
