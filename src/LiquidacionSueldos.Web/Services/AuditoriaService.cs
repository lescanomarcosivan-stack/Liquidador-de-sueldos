using LiquidacionSueldos.Web.Data;
using LiquidacionSueldos.Web.Domain;

namespace LiquidacionSueldos.Web.Services;

public interface IAuditoria
{
    /// <summary>Agrega un renglón al registro de auditoría. Se guarda junto con el resto de los cambios (SaveChanges).</summary>
    void Registrar(string accion, string detalle);
}

public class AuditoriaService(AppDbContext db, ITenantContext tenant) : IAuditoria
{
    public void Registrar(string accion, string detalle)
    {
        if (detalle.Length > 1000) detalle = detalle[..1000];
        db.Auditorias.Add(new AuditoriaEntry
        {
            EstudioId = tenant.EstudioId,
            Fecha = DateTime.UtcNow,
            Usuario = tenant.DisplayName,
            Accion = accion,
            Detalle = detalle
        });
    }
}
