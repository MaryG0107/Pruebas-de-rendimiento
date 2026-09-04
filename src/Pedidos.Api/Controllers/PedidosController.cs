using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pedidos.Api.Services;
using Pedidos.Shared.Data;
using Pedidos.Shared.Models;
using Pedidos.Shared.Validation;

namespace Pedidos.Api.Controllers;

[ApiController]
[Route("api/pedidos")]
public class PedidosController : ControllerBase
{
    private readonly PedidosDbContext _db;
    private readonly IPedidoQueuePublisher _publisher;
    private readonly ILogger<PedidosController> _logger;

    public PedidosController(PedidosDbContext db, IPedidoQueuePublisher publisher, ILogger<PedidosController> logger)
    {
        _db = db;
        _publisher = publisher;
        _logger = logger;
    }

    /// <summary>
    /// Registra un pedido y lo encola en Redis Streams para que un Worker lo procese
    /// de forma asíncrona. Responde 201 apenas el pedido queda persistido y encolado,
    /// sin esperar a que termine de procesarse.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<PedidoResponse>> CrearPedido([FromBody] PedidoCreateRequest request)
    {
        var validacion = PedidoValidator.Validar(request);
        if (!validacion.EsValido)
        {
            return BadRequest(new { errores = validacion.Errores });
        }

        var pedido = new Pedido
        {
            Cliente = request.Cliente,
            Producto = request.Producto,
            Cantidad = request.Cantidad,
            Estado = Shared.Models.EstadoPedido.Recibido
        };

        _db.Pedidos.Add(pedido);
        await _db.SaveChangesAsync();

        try
        {
            var messageId = await _publisher.PublicarAsync(pedido);
            pedido.RedisMessageId = messageId;
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Si falla el encolado, el pedido queda registrado como Error para no perderlo
            // silenciosamente. Esto es justo el tipo de escenario que el JMeter + logs debe exponer.
            _logger.LogError(ex, "Error al encolar el pedido {PedidoId}", pedido.Id);
            pedido.Estado = Shared.Models.EstadoPedido.Error;
            pedido.MensajeError = "No se pudo encolar el pedido para procesamiento.";
            await _db.SaveChangesAsync();
            return StatusCode(500, new { error = "El pedido se registró pero no pudo encolarse." });
        }

        var response = PedidoResponse.FromPedido(pedido);
        return CreatedAtAction(nameof(ObtenerPedido), new { id = pedido.Id }, response);
    }

    /// <summary>
    /// Consulta el estado actual de un pedido: recibido, procesando, completado o error.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PedidoResponse>> ObtenerPedido(Guid id)
    {
        var pedido = await _db.Pedidos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (pedido is null)
        {
            return NotFound();
        }

        return Ok(PedidoResponse.FromPedido(pedido));
    }

    /// <summary>
    /// Lista los pedidos más recientes. Pensado también como endpoint objetivo para
    /// las pruebas de carga de lectura en JMeter (consulta de pedidos pendientes).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PedidoResponse>>> ListarPedidos([FromQuery] string? estado = null)
    {
        var query = _db.Pedidos.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(estado) &&
            Enum.TryParse<Shared.Models.EstadoPedido>(estado, ignoreCase: true, out var estadoFiltro))
        {
            query = query.Where(p => p.Estado == estadoFiltro);
        }

        var pedidos = await query
            .OrderByDescending(p => p.FechaCreacion)
            .Take(200)
            .ToListAsync();

        return Ok(pedidos.Select(PedidoResponse.FromPedido));
    }
}
