using System.ComponentModel.DataAnnotations;
using LiquidacionSueldos.Web.Data;
using LiquidacionSueldos.Web.Domain;
using LiquidacionSueldos.Web.Pages.Account;
using LiquidacionSueldos.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LiquidacionSueldos.Web.Pages.Usuarios;

public class UsuarioFila
{
    public ApplicationUser User { get; set; } = null!;
    public string Rol { get; set; } = Roles.Operador;
}

public class IndexModel(UserManager<ApplicationUser> users, ITenantContext tenant, IAuditoria audit, AppDbContext db) : PageModel
{
    public class NuevoUsuario
    {
        [Required(ErrorMessage = "Ingresá el nombre.")] public string Nombre { get; set; } = "";
        [Required(ErrorMessage = "Ingresá el email."), EmailAddress(ErrorMessage = "El email no es válido.")] public string Email { get; set; } = "";
        [Required(ErrorMessage = "Ingresá una contraseña.")] public string Password { get; set; } = "";
        public string Rol { get; set; } = Roles.Operador;
    }

    public List<UsuarioFila> Lista { get; set; } = new();
    [BindProperty] public NuevoUsuario Input { get; set; } = new();
    [TempData] public string? Ok { get; set; }
    [TempData] public string? Error { get; set; }

    public async Task OnGetAsync() => await CargarAsync();

    private async Task CargarAsync()
    {
        var estudioId = tenant.EstudioId;
        var lista = await users.Users.Where(u => u.EstudioId == estudioId).OrderBy(u => u.NombreCompleto).ToListAsync();
        Lista = new();
        foreach (var u in lista)
            Lista.Add(new UsuarioFila { User = u, Rol = await users.IsInRoleAsync(u, Roles.Admin) ? Roles.Admin : Roles.Operador });
    }

    private async Task<ApplicationUser?> BuscarAsync(string id) =>
        await users.Users.FirstOrDefaultAsync(u => u.Id == id && u.EstudioId == tenant.EstudioId);

    private async Task<int> AdminsActivosAsync()
    {
        var admins = await users.GetUsersInRoleAsync(Roles.Admin);
        return admins.Count(a => a.EstudioId == tenant.EstudioId && a.Activo);
    }

    public async Task<IActionResult> OnPostCrearAsync()
    {
        if (!ModelState.IsValid) { await CargarAsync(); return Page(); }
        var email = Input.Email.Trim();
        if (await users.FindByEmailAsync(email) != null)
        {
            ModelState.AddModelError("", "Ya existe un usuario con ese email.");
            await CargarAsync(); return Page();
        }
        var rol = Input.Rol == Roles.Admin ? Roles.Admin : Roles.Operador;
        var u = new ApplicationUser
        {
            UserName = email, Email = email, EmailConfirmed = true, NombreCompleto = Input.Nombre.Trim(),
            EstudioId = tenant.EstudioId, EmpresaActivaId = tenant.EmpresaId
        };
        var res = await users.CreateAsync(u, Input.Password);
        if (!res.Succeeded)
        {
            foreach (var e in res.Errors) ModelState.AddModelError("", RegisterModel.Traducir(e));
            await CargarAsync(); return Page();
        }
        await users.AddToRoleAsync(u, rol);
        audit.Registrar("usuario_creado", $"Creó el usuario {u.Email} con rol {rol}");
        await db.SaveChangesAsync();
        Ok = "Usuario creado.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRolAsync(string id, string rol)
    {
        var u = await BuscarAsync(id);
        if (u == null) return NotFound();
        rol = rol == Roles.Admin ? Roles.Admin : Roles.Operador;
        var esAdmin = await users.IsInRoleAsync(u, Roles.Admin);
        if (esAdmin && rol == Roles.Operador && await AdminsActivosAsync() <= 1)
        {
            Error = "Debe quedar al menos un Administrador activo.";
            return RedirectToPage();
        }
        if (rol == Roles.Admin && !esAdmin) { await users.RemoveFromRoleAsync(u, Roles.Operador); await users.AddToRoleAsync(u, Roles.Admin); }
        else if (rol == Roles.Operador && esAdmin) { await users.RemoveFromRoleAsync(u, Roles.Admin); await users.AddToRoleAsync(u, Roles.Operador); }
        await users.UpdateSecurityStampAsync(u);
        audit.Registrar("usuario_rol", $"Cambió el rol de {u.Email} a {rol}");
        await db.SaveChangesAsync();
        Ok = "Rol actualizado.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostActivoAsync(string id)
    {
        var u = await BuscarAsync(id);
        if (u == null) return NotFound();
        if (u.Id == tenant.UserId) { Error = "No podés desactivar tu propio usuario."; return RedirectToPage(); }
        if (u.Activo && await users.IsInRoleAsync(u, Roles.Admin) && await AdminsActivosAsync() <= 1)
        {
            Error = "Debe quedar al menos un Administrador activo.";
            return RedirectToPage();
        }
        u.Activo = !u.Activo;
        await users.UpdateAsync(u);
        if (!u.Activo) await users.UpdateSecurityStampAsync(u); // cierra sus sesiones abiertas
        audit.Registrar(u.Activo ? "usuario_activado" : "usuario_desactivado", $"{(u.Activo ? "Activó" : "Desactivó")} al usuario {u.Email}");
        await db.SaveChangesAsync();
        Ok = u.Activo ? "Usuario activado." : "Usuario desactivado.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostClaveAsync(string id, string password)
    {
        var u = await BuscarAsync(id);
        if (u == null) return NotFound();
        var token = await users.GeneratePasswordResetTokenAsync(u);
        var res = await users.ResetPasswordAsync(u, token, password ?? "");
        if (!res.Succeeded) { Error = string.Join(" ", res.Errors.Select(RegisterModel.Traducir)); return RedirectToPage(); }
        audit.Registrar("usuario_clave", $"Restableció la contraseña de {u.Email}");
        await db.SaveChangesAsync();
        Ok = "Contraseña actualizada.";
        return RedirectToPage();
    }
}
