using LiquidacionSueldos.Web.Domain;
using Xunit;

namespace LiquidacionSueldos.Tests;

public class PlanesTests
{
    private static readonly DateTime Alta = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void LosPlanesPagosCrecenDeBasicoAIlimitado()
    {
        var pagos = Planes.Todos.Where(p => p.Codigo != Planes.Prueba).ToArray();
        for (var i = 1; i < pagos.Length; i++)
        {
            Assert.True(pagos[i].MaxEmpresas >= pagos[i - 1].MaxEmpresas);
            Assert.True(pagos[i].MaxEmpleados >= pagos[i - 1].MaxEmpleados);
            Assert.True(pagos[i].MaxUsuarios >= pagos[i - 1].MaxUsuarios);
        }
    }

    [Fact]
    public void LaPruebaNoLimitaCantidadesSoloTiempo()
    {
        var prueba = Planes.Obtener(Planes.Prueba);
        Assert.False(Planes.AlcanzoElLimite(prueba, Recurso.Empresas, 500));
        Assert.False(Planes.AlcanzoElLimite(prueba, Recurso.Empleados, 5000));
        Assert.False(Planes.AlcanzoElLimite(prueba, Recurso.Usuarios, 100));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(2, false)]
    [InlineData(3, true)]   // plan Básico: 3 empresas, la cuarta no entra
    [InlineData(4, true)]
    public void ElLimiteSeAlcanzaAlLlegarAlTope(int actuales, bool esperado)
    {
        var basico = Planes.Obtener(Planes.Basico);
        Assert.Equal(esperado, Planes.AlcanzoElLimite(basico, Recurso.Empresas, actuales));
    }

    [Fact]
    public void ElPlanIlimitadoNuncaBloquea()
    {
        var ilimitado = Planes.Obtener(Planes.Ilimitado);
        Assert.False(Planes.AlcanzoElLimite(ilimitado, Recurso.Empleados, 1_000_000));
        Assert.Equal("sin límite", Planes.Etiqueta(ilimitado.MaxEmpleados));
    }

    [Fact]
    public void UnCodigoDesconocidoCaeEnPrueba()
    {
        Assert.Equal(Planes.Prueba, Planes.Obtener("loquesea").Codigo);
        Assert.Equal(Planes.Prueba, Planes.Obtener(null).Codigo);
        Assert.False(Planes.Existe("loquesea"));
        Assert.True(Planes.Existe("basico")); // sin distinguir mayúsculas
    }

    [Fact]
    public void ElMensajeDeLimiteNombraElPlanYElTope()
    {
        var msg = Planes.MensajeLimite(Planes.Obtener(Planes.Basico), Recurso.Empleados);
        Assert.Contains("Básico", msg);
        Assert.Contains("50", msg);
        Assert.Contains("empleados", msg);
    }

    // ---------- Prueba gratuita de 30 días ----------

    [Fact]
    public void LaPruebaDuraTreintaDiasDesdeElAlta()
    {
        Assert.Equal(30, Planes.DiasPrueba);
        var dia0 = Planes.CalcularPrueba(Planes.Prueba, Alta, null, Alta);
        Assert.True(dia0.EnPrueba);
        Assert.False(dia0.Vencida);
        Assert.Equal(30, dia0.DiasRestantes);
        Assert.Equal(Alta.AddDays(30), dia0.Vence);
    }

    [Fact]
    public void LosDiasRestantesBajanYLaPruebaVenceAlCumplirseElPlazo()
    {
        Assert.Equal(1, Planes.CalcularPrueba(Planes.Prueba, Alta, null, Alta.AddDays(29)).DiasRestantes);
        Assert.Equal(1, Planes.CalcularPrueba(Planes.Prueba, Alta, null, Alta.AddDays(29.5)).DiasRestantes); // medio día cuenta como 1

        var justo = Planes.CalcularPrueba(Planes.Prueba, Alta, null, Alta.AddDays(30));
        Assert.True(justo.Vencida);
        Assert.Equal(0, justo.DiasRestantes);

        Assert.True(Planes.CalcularPrueba(Planes.Prueba, Alta, null, Alta.AddDays(90)).Vencida);
    }

    [Fact]
    public void UnVencimientoCargadoTienePrioridadSobreElAlta()
    {
        var extendida = Alta.AddDays(60);
        var e = Planes.CalcularPrueba(Planes.Prueba, Alta, extendida, Alta.AddDays(45));
        Assert.False(e.Vencida);
        Assert.Equal(15, e.DiasRestantes);
        Assert.Equal(extendida, e.Vence);
    }

    [Theory]
    [InlineData(Planes.Basico)]
    [InlineData(Planes.Estudio)]
    [InlineData(Planes.Ilimitado)]
    public void LosPlanesPagosNoTienenVencimiento(string plan)
    {
        var e = Planes.CalcularPrueba(plan, Alta, null, Alta.AddDays(1000));
        Assert.False(e.EnPrueba);
        Assert.False(e.Vencida);
    }
}
