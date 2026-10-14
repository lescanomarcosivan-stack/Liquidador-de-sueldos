using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;

namespace LiquidacionSueldos.Web.Services;

/// <summary>Envío de correos transaccionales (recuperación de contraseña, etc.).</summary>
public interface ICorreo
{
    /// <summary>Devuelve true si el proveedor aceptó el mensaje. Nunca lanza excepciones.</summary>
    Task<bool> EnviarAsync(string para, string asunto, string html);
}

/// <summary>
/// Envía por la API HTTP de Resend (https://resend.com). Se usa HTTP y no SMTP porque Railway bloquea
/// los puertos SMTP en los planes Hobby/Free. Variables de entorno: RESEND_API_KEY y EMAIL_FROM
/// (ej. "Liquidación de Sueldos &lt;avisos@tudominio.com&gt;"; sin dominio verificado se puede probar con
/// "onboarding@resend.dev", que solo entrega al email dueño de la cuenta de Resend).
/// </summary>
public class CorreoResend(HttpClient http, IConfiguration config, ILogger<CorreoResend> log) : ICorreo
{
    public async Task<bool> EnviarAsync(string para, string asunto, string html)
    {
        var key = config["RESEND_API_KEY"];
        var from = config["EMAIL_FROM"];
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(from))
        {
            log.LogWarning("No se envió el correo '{Asunto}': faltan RESEND_API_KEY o EMAIL_FROM.", asunto);
            return false;
        }

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails")
            {
                Content = JsonContent.Create(new { from, to = new[] { para }, subject = asunto, html })
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            using var res = await http.SendAsync(req);
            if (res.IsSuccessStatusCode) return true;
            log.LogError("Resend rechazó el correo ({Codigo}): {Cuerpo}", (int)res.StatusCode, await res.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Falló el envío de correo.");
        }
        return false;
    }
}

/// <summary>Mensajes de error de Identity en español.</summary>
public class IdentityErrorsEs : IdentityErrorDescriber
{
    public override IdentityError PasswordTooShort(int length) => new() { Code = nameof(PasswordTooShort), Description = $"La contraseña debe tener al menos {length} caracteres." };
    public override IdentityError PasswordRequiresDigit() => new() { Code = nameof(PasswordRequiresDigit), Description = "La contraseña debe tener al menos un número." };
    public override IdentityError PasswordRequiresLower() => new() { Code = nameof(PasswordRequiresLower), Description = "La contraseña debe tener al menos una minúscula." };
    public override IdentityError PasswordRequiresUpper() => new() { Code = nameof(PasswordRequiresUpper), Description = "La contraseña debe tener al menos una mayúscula." };
    public override IdentityError PasswordRequiresNonAlphanumeric() => new() { Code = nameof(PasswordRequiresNonAlphanumeric), Description = "La contraseña debe tener al menos un símbolo." };
    public override IdentityError InvalidToken() => new() { Code = nameof(InvalidToken), Description = "El enlace no es válido o venció. Pedí uno nuevo." };
    public override IdentityError DuplicateEmail(string email) => new() { Code = nameof(DuplicateEmail), Description = $"El email {email} ya está registrado." };
    public override IdentityError DuplicateUserName(string userName) => new() { Code = nameof(DuplicateUserName), Description = $"El usuario {userName} ya existe." };
    public override IdentityError InvalidEmail(string? email) => new() { Code = nameof(InvalidEmail), Description = "El email no es válido." };
}
