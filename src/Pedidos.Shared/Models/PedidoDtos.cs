namespace Pedidos.Shared.Models;

/// <summary>
/// Payload que envía el cliente al crear un pedido (POST /api/pedidos).
/// </summary>
public class PedidoCreateRequest
{
    public string Cliente { get; set; } = string.Empty;
    public string Producto { get; set; } = string.Empty;
    public int Cantidad { get; set; }
}

/// <summary>
/// Respuesta que se devuelve al consultar el estado de un pedido (GET /api/pedidos/{id}).
/// </summary>
public class PedidoResponse
{
    public Guid Id { get; set; }
    public string Cliente { get; set; } = string.Empty;
    public string Producto { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string? MensajeError { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime FechaActualizacion { get; set; }

    public static PedidoResponse FromPedido(Pedido pedido) => new()
    {
        Id = pedido.Id,
        Cliente = pedido.Cliente,
        Producto = pedido.Producto,
        Cantidad = pedido.Cantidad,
        Estado = pedido.Estado.ToString(),
        MensajeError = pedido.MensajeError,
        FechaCreacion = pedido.FechaCreacion,
        FechaActualizacion = pedido.FechaActualizacion
    };
}
