using LiquidacionSueldos.Web.Domain;
using Xunit;

namespace LiquidacionSueldos.Tests;

public class PlanesTests
{
    [Fact]
    public void LosPlanesCrecenDePruebaAIlimitado()
    {
        var p = Planes.Todos;
        for (var i = 1; i < p.Length; i++)
        {
            Assert.True(p[i].MaxEmpresas >= p[i - 1].MaxEmpresas);
            Assert.True(p[i].MaxEmpleados >= p[i - 1].MaxEmpleados);
            Assert.True(p[i].MaxUsuarios >= p[i - 1].MaxUsuarios);
        }
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
    public void UnCodigoDesconocidoCaeEnElPlanMasRestrictivo()
    {
        Assert.Equal(Planes.Prueba, Planes.Obtener("loquesea").Codigo);
        Assert.Equal(Planes.Prueba, Planes.Obtener(null).Codigo);
        Assert.False(Planes.Existe("loquesea"));
        Assert.True(Planes.Existe("basico")); // sin distinguir mayúsculas
    }

    [Fact]
    public void ElMensajeDeLimiteNombraElPlanYElTope()
    {
        var msg = Planes.MensajeLimite(Planes.Obtener(Planes.Prueba), Recurso.Empleados);
        Assert.Contains("Prueba", msg);
        Assert.Contains("10", msg);
        Assert.Contains("empleados", msg);
    }
}
