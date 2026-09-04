namespace Pedidos.Shared;

/// <summary>
/// Nombres compartidos del stream y del grupo de consumidores de Redis Streams.
/// Centralizarlos aquí evita typos entre la Api (productora) y el Worker (consumidor).
/// </summary>
public static class RedisPedidosConstants
{
    public const string StreamKey = "pedidos-stream";
    public const string ConsumerGroup = "pedidos-workers";

    // Nombres de los campos dentro de cada mensaje del stream
    public const string FieldPedidoId = "pedidoId";
    public const string FieldCliente = "cliente";
    public const string FieldProducto = "producto";
    public const string FieldCantidad = "cantidad";
}
