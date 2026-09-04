using Pedidos.Shared;
using Pedidos.Shared.Models;
using StackExchange.Redis;

namespace Pedidos.Api.Services;

/// <summary>
/// Publica cada pedido nuevo como un mensaje en el Redis Stream "pedidos-stream".
/// El Worker Service consume de este mismo stream usando un consumer group,
/// lo que permite escalar a varios workers sin procesar el mismo pedido dos veces.
/// </summary>
public class RedisPedidoQueuePublisher : IPedidoQueuePublisher
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisPedidoQueuePublisher> _logger;

    public RedisPedidoQueuePublisher(IConnectionMultiplexer redis, ILogger<RedisPedidoQueuePublisher> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<string> PublicarAsync(Pedido pedido, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();

        var entries = new NameValueEntry[]
        {
            new(RedisPedidosConstants.FieldPedidoId, pedido.Id.ToString()),
            new(RedisPedidosConstants.FieldCliente, pedido.Cliente),
            new(RedisPedidosConstants.FieldProducto, pedido.Producto),
            new(RedisPedidosConstants.FieldCantidad, pedido.Cantidad)
        };

        var messageId = await db.StreamAddAsync(RedisPedidosConstants.StreamKey, entries);

        _logger.LogInformation(
            "Pedido {PedidoId} publicado en {Stream} con messageId {MessageId}",
            pedido.Id, RedisPedidosConstants.StreamKey, messageId);

        return messageId.ToString();
    }
}
