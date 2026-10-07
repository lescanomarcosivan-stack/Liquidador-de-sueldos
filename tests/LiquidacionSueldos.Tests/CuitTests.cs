using LiquidacionSueldos.Web.Domain;
using Xunit;

namespace LiquidacionSueldos.Tests;

public class CuitTests
{
    [Theory]
    [InlineData("20-12345678-6")]
    [InlineData("20123456786")]
    [InlineData("30-71234567-1")]
    public void Acepta_cuit_con_digito_verificador_correcto(string valor) => Assert.True(Cuit.EsValido(valor));

    [Theory]
    [InlineData("20-12345678-5")]
    [InlineData("")]
    [InlineData("123")]
    [InlineData(null)]
    public void Rechaza_cuit_invalido(string? valor) => Assert.False(Cuit.EsValido(valor));

    [Fact]
    public void Formatea_con_guiones() => Assert.Equal("20-12345678-6", Cuit.Formatear("20123456786"));
}
