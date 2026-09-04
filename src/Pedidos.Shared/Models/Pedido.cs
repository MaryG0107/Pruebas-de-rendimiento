namespace Pedidos.Shared.Models;

/// <summary>
/// Entidad principal del dominio. Representa un pedido registrado por un cliente
/// que será procesado de forma asíncrona a través de Redis Streams y un Worker Service.
/// </summary>
public class Pedido
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Cliente { get; set; } = string.Empty;

    public string Producto { get; set; } = string.Empty;

    public int Cantidad { get; set; }

    public EstadoPedido Estado { get; set; } = EstadoPedido.Recibido;

    public string? MensajeError { get; set; }

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public DateTime FechaActualizacion { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Id del mensaje dentro del stream de Redis (formato "timestamp-secuencia").
    /// Útil para trazabilidad y para pruebas de pérdida/duplicación de mensajes.
    /// </summary>
    public string? RedisMessageId { get; set; }
}
