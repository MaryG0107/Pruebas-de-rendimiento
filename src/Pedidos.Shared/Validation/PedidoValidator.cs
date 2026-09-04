using Pedidos.Shared.Models;

namespace Pedidos.Shared.Validation;

/// <summary>
/// Resultado de validar un PedidoCreateRequest: si es válido y, si no, la lista de errores.
/// </summary>
public class ResultadoValidacion
{
    public bool EsValido => Errores.Count == 0;
    public List<string> Errores { get; } = new();
}

/// <summary>
/// Reglas de negocio para la creación de pedidos. Esta clase vive en Pedidos.Shared
/// precisamente para poder probarla con pruebas unitarias puras, sin levantar la API,
/// la base de datos ni Redis (ver Pedidos.Tests.Unit).
/// </summary>
public static class PedidoValidator
{
    public const int CantidadMaxima = 1000;

    public static ResultadoValidacion Validar(PedidoCreateRequest request)
    {
        var resultado = new ResultadoValidacion();

        if (request is null)
        {
            resultado.Errores.Add("La solicitud no puede ser nula.");
            return resultado;
        }

        if (string.IsNullOrWhiteSpace(request.Cliente))
        {
            resultado.Errores.Add("El campo 'cliente' es obligatorio.");
        }
        else if (request.Cliente.Length > 100)
        {
            resultado.Errores.Add("El campo 'cliente' no puede superar 100 caracteres.");
        }

        if (string.IsNullOrWhiteSpace(request.Producto))
        {
            resultado.Errores.Add("El campo 'producto' es obligatorio.");
        }

        if (request.Cantidad <= 0)
        {
            resultado.Errores.Add("La cantidad debe ser mayor a cero.");
        }
        else if (request.Cantidad > CantidadMaxima)
        {
            resultado.Errores.Add($"La cantidad no puede superar {CantidadMaxima} unidades por pedido.");
        }

        return resultado;
    }
}
