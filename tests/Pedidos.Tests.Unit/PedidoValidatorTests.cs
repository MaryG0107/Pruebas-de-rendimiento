using Pedidos.Shared.Models;
using Pedidos.Shared.Validation;
using Xunit;

namespace Pedidos.Tests.Unit;

/// <summary>
/// Pruebas unitarias de la lógica de negocio del pedido (TC-01 a TC-06 de la matriz de casos).
/// No dependen de Redis, Postgres ni de la API: prueban PedidoValidator de forma aislada.
/// </summary>
public class PedidoValidatorTests
{
    private static PedidoCreateRequest PedidoValido() => new()
    {
        Cliente = "Mary Sagui",
        Producto = "Laptop Dell",
        Cantidad = 2
    };

    [Fact(DisplayName = "TC-01: Un pedido con todos los campos válidos no genera errores")]
    public void Validar_ConDatosValidos_NoDevuelveErrores()
    {
        var resultado = PedidoValidator.Validar(PedidoValido());

        Assert.True(resultado.EsValido);
        Assert.Empty(resultado.Errores);
    }

    [Theory(DisplayName = "TC-02: Cliente vacío o solo espacios es inválido")]
    [InlineData("")]
    [InlineData("   ")]
    public void Validar_ClienteVacio_EsInvalido(string cliente)
    {
        var request = PedidoValido();
        request.Cliente = cliente;

        var resultado = PedidoValidator.Validar(request);

        Assert.False(resultado.EsValido);
        Assert.Contains(resultado.Errores, e => e.Contains("cliente"));
    }

    [Fact(DisplayName = "TC-03: Producto vacío es inválido")]
    public void Validar_ProductoVacio_EsInvalido()
    {
        var request = PedidoValido();
        request.Producto = "";

        var resultado = PedidoValidator.Validar(request);

        Assert.False(resultado.EsValido);
        Assert.Contains(resultado.Errores, e => e.Contains("producto"));
    }

    [Theory(DisplayName = "TC-04: Cantidad cero o negativa es inválida (boundary value analysis)")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Validar_CantidadNoPositiva_EsInvalida(int cantidad)
    {
        var request = PedidoValido();
        request.Cantidad = cantidad;

        var resultado = PedidoValidator.Validar(request);

        Assert.False(resultado.EsValido);
        Assert.Contains(resultado.Errores, e => e.Contains("cantidad debe ser mayor a cero"));
    }

    [Fact(DisplayName = "TC-05: Cantidad justo en el límite máximo permitido es válida (boundary value analysis)")]
    public void Validar_CantidadEnElLimiteMaximo_EsValida()
    {
        var request = PedidoValido();
        request.Cantidad = PedidoValidator.CantidadMaxima;

        var resultado = PedidoValidator.Validar(request);

        Assert.True(resultado.EsValido);
    }

    [Fact(DisplayName = "TC-06: Cantidad un paso por encima del límite máximo es inválida (boundary value analysis)")]
    public void Validar_CantidadSobreElLimiteMaximo_EsInvalida()
    {
        var request = PedidoValido();
        request.Cantidad = PedidoValidator.CantidadMaxima + 1;

        var resultado = PedidoValidator.Validar(request);

        Assert.False(resultado.EsValido);
        Assert.Contains(resultado.Errores, e => e.Contains("no puede superar"));
    }

    [Fact(DisplayName = "TC-07: Un pedido con varios campos inválidos acumula todos los errores")]
    public void Validar_ConMultiplesCamposInvalidos_AcumulaTodosLosErrores()
    {
        var request = new PedidoCreateRequest
        {
            Cliente = "",
            Producto = "",
            Cantidad = -5
        };

        var resultado = PedidoValidator.Validar(request);

        Assert.False(resultado.EsValido);
        Assert.Equal(3, resultado.Errores.Count);
    }
}
