using Pedidos.Shared.Models;

namespace Pedidos.Api.Services;

/// <summary>
/// Abstracción sobre el mecanismo de cola/mensajería usado para encolar pedidos.
/// Separarla de la implementación concreta (Redis Streams) permite:
///  - probar el controlador con un mock en pruebas de integración ligeras,
///  - cambiar de mecanismo de cola sin tocar el controlador.
/// </summary>
public interface IPedidoQueuePublisher
{
    /// <summary>
    /// Publica el pedido en el stream y devuelve el id del mensaje asignado por Redis.
    /// </summary>
    Task<string> PublicarAsync(Pedido pedido, CancellationToken cancellationToken = default);
}
