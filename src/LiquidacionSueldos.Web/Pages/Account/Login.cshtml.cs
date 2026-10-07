using System.ComponentModel.DataAnnotations;
using LiquidacionSueldos.Web.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LiquidacionSueldos.Web.Pages.Account;

public class LoginModel(SignInManager<ApplicationUser> signIn, UserManager<ApplicationUser> users) : PageModel
{
    public class InputModel
    {
        [Required, EmailAddress] public string Email { get; set; } = "";
        [Required] public string Password { get; set; } = "";
    }

    [BindProperty] public InputModel Input { get; set; } = new();
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }
    public string? Error { get; set; }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToPage("/Index");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) { Error = "Completá email y contraseña."; return Page(); }

        var user = await users.FindByEmailAsync(Input.Email.Trim());
        if (user == null || !user.Activo)
        {
            Error = "Email o contraseña incorrectos.";
            return Page();
        }

        var res = await signIn.PasswordSignInAsync(user, Input.Password, isPersistent: true, lockoutOnFailure: true);
        if (res.IsLockedOut) { Error = "Demasiados intentos fallidos. Probá de nuevo en unos minutos."; return Page(); }
        if (!res.Succeeded) { Error = "Email o contraseña incorrectos."; return Page(); }

        if (!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl)) return LocalRedirect(ReturnUrl);
        return RedirectToPage("/Index");
    }
}
