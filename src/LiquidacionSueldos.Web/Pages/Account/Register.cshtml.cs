using System.ComponentModel.DataAnnotations;
using LiquidacionSueldos.Web.Data;
using LiquidacionSueldos.Web.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LiquidacionSueldos.Web.Pages.Account;

public class RegisterModel(AppDbContext db, UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn) : PageModel
{
    public class InputModel
    {
        [Required(ErrorMessage = "Ingresá el nombre del estudio."), MaxLength(150)] public string Estudio { get; set; } = "";
        [Required(ErrorMessage = "Ingresá tu nombre."), MaxLength(150)] public string Nombre { get; set; } = "";
        [Required(ErrorMessage = "Ingresá un email."), EmailAddress(ErrorMessage = "El email no es válido.")] public string Email { get; set; } = "";
        [Required(ErrorMessage = "Ingresá una contraseña.")] public string Password { get; set; } = "";
        [Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")] public string Confirmar { get; set; } = "";
    }

    [BindProperty] public InputModel Input { get; set; } = new();

    public IActionResult OnGet() => User.Identity?.IsAuthenticated == true ? RedirectToPage("/Index") : Page();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var email = Input.Email.Trim();
        if (await users.FindByEmailAsync(email) != null)
        {
            ModelState.AddModelError("", "Ya existe un usuario con ese email.");
            return Page();
        }

        var estudio = new Estudio { Nombre = Input.Estudio.Trim() };
        var user = new ApplicationUser
        {
            UserName = email, Email = email, EmailConfirmed = true,
            NombreCompleto = Input.Nombre.Trim(), EstudioId = estudio.Id
        };

        var res = await users.CreateAsync(user, Input.Password);
        if (!res.Succeeded)
        {
            foreach (var e in res.Errors) ModelState.AddModelError("", Traducir(e));
            return Page();
        }
        await users.AddToRoleAsync(user, Roles.Admin);

        db.Estudios.Add(estudio);
        DefaultsSeeder.Sembrar(db, estudio.Id);
        db.Auditorias.Add(new AuditoriaEntry
        {
            EstudioId = estudio.Id, Usuario = user.NombreCompleto, Accion = "estudio_creado",
            Detalle = $"Se creó el estudio «{estudio.Nombre}»"
        });
        await db.SaveChangesAsync();

        await signIn.SignInAsync(user, isPersistent: true);
        return RedirectToPage("/SinEmpresa");
    }

    public static string Traducir(IdentityError e) => e.Code switch
    {
        "PasswordTooShort" => "La contraseña debe tener al menos 8 caracteres.",
        "PasswordRequiresDigit" => "La contraseña debe incluir un número.",
        "PasswordRequiresLower" => "La contraseña debe incluir una minúscula.",
        "DuplicateUserName" or "DuplicateEmail" => "Ya existe un usuario con ese email.",
        "InvalidEmail" or "InvalidUserName" => "El email no es válido.",
        _ => e.Description
    };
}
