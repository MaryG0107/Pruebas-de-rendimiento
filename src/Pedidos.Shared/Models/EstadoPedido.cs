namespace Pedidos.Shared.Models;

/// <summary>
/// Estados posibles de un pedido a lo largo de su ciclo de vida.
/// Recibido: creado en la API, encolado en Redis Streams.
/// Procesando: tomado por un Worker y en ejecución.
/// Completado: procesado exitosamente.
/// Error: el Worker encontró un problema al procesarlo.
/// </summary>
public enum EstadoPedido
{
    Recibido = 0,
    Procesando = 1,
    Completado = 2,
    Error = 3
}
