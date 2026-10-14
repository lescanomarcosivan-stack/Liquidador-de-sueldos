using System.ComponentModel.DataAnnotations;
using System.Text;
using LiquidacionSueldos.Web.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace LiquidacionSueldos.Web.Pages.Account;

public class RestablecerClaveModel(UserManager<ApplicationUser> users) : PageModel
{
    [BindProperty(SupportsGet = true)] public string Email { get; set; } = "";
    [BindProperty(SupportsGet = true)] public string Codigo { get; set; } = "";
    [BindProperty, Required(ErrorMessage = "Ingresá la contraseña nueva.")] public string Clave { get; set; } = "";
    [BindProperty, Required(ErrorMessage = "Repetí la contraseña nueva.")] public string Confirmar { get; set; } = "";
    public bool Listo { get; set; }
    public bool EnlaceInvalido => string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Codigo);

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (EnlaceInvalido) return Page();
        if (!ModelState.IsValid) return Page();
        if (Clave != Confirmar) { ModelState.AddModelError("", "Las contraseñas no coinciden."); return Page(); }

        string token;
        try { token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(Codigo)); }
        catch (FormatException) { ModelState.AddModelError("", "El enlace no es válido o venció. Pedí uno nuevo."); return Page(); }

        var user = await users.FindByEmailAsync(Email.Trim());
        if (user == null || !user.Activo)
        {
            ModelState.AddModelError("", "El enlace no es válido o venció. Pedí uno nuevo.");
            return Page();
        }

        var res = await users.ResetPasswordAsync(user, token, Clave);
        if (!res.Succeeded)
        {
            foreach (var e in res.Errors) ModelState.AddModelError("", e.Description);
            return Page();
        }

        // Si la cuenta estaba bloqueada por intentos fallidos, se libera: acaba de probar que controla el email.
        await users.SetLockoutEndDateAsync(user, null);
        await users.ResetAccessFailedCountAsync(user);
        Listo = true;
        return Page();
    }
}
