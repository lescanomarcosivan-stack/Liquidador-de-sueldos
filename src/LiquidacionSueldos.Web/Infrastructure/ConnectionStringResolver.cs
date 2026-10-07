using System.Text;

namespace LiquidacionSueldos.Web.Infrastructure;

/// <summary>
/// Resuelve la cadena de conexión a PostgreSQL. Orden de prioridad:
/// 1) variable DATABASE_URL (formato postgres://usuario:clave@host:puerto/base — la que provee Railway),
/// 2) ConnectionStrings:Default (appsettings o variable ConnectionStrings__Default).
/// </summary>
public static class ConnectionStringResolver
{
    public static string Resolve(IConfiguration config)
    {
        var url = config["DATABASE_URL"];
        if (!string.IsNullOrWhiteSpace(url)) return FromUrl(url);

        var cs = config.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(cs))
            throw new InvalidOperationException(
                "No hay base de datos configurada. Definí DATABASE_URL o ConnectionStrings:Default.");
        return cs;
    }

    public static string FromUrl(string url)
    {
        var uri = new Uri(url);
        var userInfo = uri.UserInfo.Split(':', 2);
        var user = Uri.UnescapeDataString(userInfo[0]);
        var pass = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
        var db = uri.AbsolutePath.TrimStart('/');
        var port = uri.Port > 0 ? uri.Port : 5432;

        var sb = new StringBuilder();
        sb.Append($"Host={uri.Host};Port={port};Database={db};Username={user};Password={pass};");
        sb.Append("SSL Mode=Prefer;Trust Server Certificate=true");
        return sb.ToString();
    }
}
