using LiquidacionSueldos.Web.Data;
using LiquidacionSueldos.Web.Domain;
using LiquidacionSueldos.Web.Infrastructure;
using LiquidacionSueldos.Web.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Railway/Docker informan el puerto por la variable PORT.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var connectionString = ConnectionStringResolver.Resolve(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<IAuditoria, AuditoriaService>();
builder.Services.AddScoped<LiquidacionService>();
builder.Services.AddScoped<EmpresaActivaService>();

builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connectionString));

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(o =>
    {
        o.Password.RequiredLength = 8;
        o.Password.RequireNonAlphanumeric = false;
        o.Password.RequireUppercase = false;
        o.User.RequireUniqueEmail = true;
        o.Lockout.MaxFailedAccessAttempts = 5;
        o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders()
    .AddClaimsPrincipalFactory<AppClaimsFactory>();

builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath = "/Account/Login";
    o.AccessDeniedPath = "/Account/Login";
    o.ExpireTimeSpan = TimeSpan.FromHours(12);
    o.SlidingExpiration = true;
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Lax;
});

// Las claves de cifrado de cookies se guardan en la base: sobreviven a los redeploys.
builder.Services.AddDataProtection()
    .SetApplicationName("LiquidacionSueldos")
    .PersistKeysToDbContext<AppDbContext>();

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Admin", p => p.RequireRole(Roles.Admin));

builder.Services.AddRazorPages(o =>
{
    o.Conventions.AuthorizeFolder("/");
    o.Conventions.AllowAnonymousToFolder("/Account");
    o.Conventions.AllowAnonymousToPage("/Error");
    o.Conventions.AuthorizeFolder("/Usuarios", "Admin");
})
.AddMvcOptions(o =>
{
    o.Filters.Add<EmpresaRequeridaFilter>();
    // Mensajes de validación de números/valores en español (por defecto salen en inglés).
    var m = o.ModelBindingMessageProvider;
    m.SetValueMustBeANumberAccessor(campo => $"El campo {campo} debe ser un número.");
    m.SetAttemptedValueIsInvalidAccessor((valor, campo) => $"El valor '{valor}' no es válido para {campo}.");
    m.SetValueIsInvalidAccessor(valor => $"El valor '{valor}' no es válido.");
    m.SetMissingBindRequiredValueAccessor(campo => $"Falta el valor de {campo}.");
});

var app = builder.Build();

await InicializarBaseAsync(app);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.MapGet("/healthz", () => Results.Ok("ok")).AllowAnonymous();

app.Run();

static async Task InicializarBaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var log = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // La base puede tardar unos segundos en estar disponible (Railway, docker compose).
    for (var intento = 1; ; intento++)
    {
        try
        {
            if (db.Database.GetMigrations().Any()) await db.Database.MigrateAsync();
            else await db.Database.EnsureCreatedAsync();
            break;
        }
        catch (Exception ex) when (intento < 10)
        {
            log.LogWarning("Base de datos no disponible (intento {N}/10): {Msg}", intento, ex.Message);
            await Task.Delay(TimeSpan.FromSeconds(3));
        }
    }

    var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    foreach (var rol in new[] { Roles.Admin, Roles.Operador })
        if (!await roles.RoleExistsAsync(rol))
            await roles.CreateAsync(new IdentityRole(rol));
}
