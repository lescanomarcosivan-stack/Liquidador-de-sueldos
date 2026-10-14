using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Text;
using LiquidacionSueldos.Web.Domain;
using LiquidacionSueldos.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace LiquidacionSueldos.Web.Pages.Account;

public class OlvideClaveModel(UserManager<ApplicationUser> users, ICorreo correo) : PageModel
{
    /// <summary>Evita que alguien dispare correos repetidos a una misma casilla (mínimo 2 minutos entre envíos).</summary>
    private static readonly ConcurrentDictionary<string, DateTime> UltimoEnvio = new();

    [BindProperty, Required(ErrorMessage = "Ingresá tu email."), EmailAddress(ErrorMessage = "El email no es válido.")]
    public string Email { get; set; } = "";
    public bool Enviado { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var email = Email.Trim();
        var user = await users.FindByEmailAsync(email);
        var clave = email.ToLowerInvariant();
        var reciente = UltimoEnvio.TryGetValue(clave, out var t) && DateTime.UtcNow - t < TimeSpan.FromMinutes(2);

        if (user is { Activo: true } && !reciente)
        {
            UltimoEnvio[clave] = DateTime.UtcNow;
            var token = await users.GeneratePasswordResetTokenAsync(user);
            var codigo = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var enlace = Url.Page("/Account/RestablecerClave", null, new { email = user.Email, codigo }, Request.Scheme)!;
            var html = $"""
                <p>Hola {System.Net.WebUtility.HtmlEncode(user.NombreCompleto)},</p>
                <p>Recibimos un pedido para restablecer tu contraseña en <strong>Liquidación de Sueldos</strong>.</p>
                <p><a href="{enlace}">Elegir una contraseña nueva</a></p>
                <p>El enlace vence en 1 hora. Si no fuiste vos, ignorá este mensaje: tu contraseña no cambia.</p>
                """;
            await correo.EnviarAsync(user.Email!, "Restablecer tu contraseña", html);
        }

        // Mismo mensaje exista o no la cuenta, para no revelar qué emails están registrados.
        Enviado = true;
        return Page();
    }
}
