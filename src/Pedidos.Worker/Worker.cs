using Microsoft.EntityFrameworkCore;
using Pedidos.Shared;
using Pedidos.Shared.Data;
using Pedidos.Shared.Models;
using StackExchange.Redis;

namespace Pedidos.Worker;

/// <summary>
/// Consume pedidos del stream "pedidos-stream" usando el consumer group "pedidos-workers"
/// y los procesa: Recibido -> Procesando -> Completado (o Error si algo falla).
///
/// Puntos importantes para las pruebas de resiliencia del reto:
///  - Si este proceso se detiene, los mensajes NO se pierden: quedan pendientes en el
///    stream (visibles con XPENDING) hasta que un consumidor los reclame o los procese.
///  - Al reiniciar, retoma leyendo mensajes nuevos con XREADGROUP; los mensajes que
///    quedaron "pending" de una ejecución anterior se reclaman al inicio con
///    ReclamarPendientesAsync, evitando perderlos.
///  - Cada mensaje se confirma (XACK) solo después de persistir el resultado en la
///    base de datos, para minimizar la ventana en la que un pedido podría quedar
///    "a medias" si el Worker se cae justo durante el procesamiento.
/// </summary>
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConnectionMultiplexer _redis;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly string _consumerName;

    // Simula el "trabajo" de procesar un pedido (validaciones externas, cobro, etc.)
    private static readonly Random _random = new();

    public Worker(
        ILogger<Worker> logger,
        IConnectionMultiplexer redis,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _redis = redis;
        _scopeFactory = scopeFactory;
        _consumerName = $"worker-{Environment.MachineName}-{Environment.ProcessId}";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var db = _redis.GetDatabase();
        await AsegurarGrupoConsumidorAsync(db);

        _logger.LogInformation(
            "Worker {ConsumerName} iniciado. Escuchando '{Stream}' en el grupo '{Group}'.",
            _consumerName, RedisPedidosConstants.StreamKey, RedisPedidosConstants.ConsumerGroup);

        // Al arrancar, reclama cualquier mensaje que haya quedado "pending" de una
        // ejecución anterior del Worker (por ejemplo, si se detuvo abruptamente).
        await ReclamarPendientesAsync(db, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var entries = await db.StreamReadGroupAsync(
                    RedisPedidosConstants.StreamKey,
                    RedisPedidosConstants.ConsumerGroup,
                    _consumerName,
                    position: ">", // solo mensajes nunca entregados a ningún consumidor
                    count: 10);

                if (entries.Length == 0)
                {
                    // No hay mensajes nuevos: esperar un poco antes de volver a preguntar.
                    await Task.Delay(TimeSpan.FromMilliseconds(500), stoppingToken);
                    continue;
                }

                foreach (var entry in entries)
                {
                    await ProcesarMensajeAsync(db, entry, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado en el loop principal del Worker.");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }

        _logger.LogInformation("Worker {ConsumerName} detenido.", _consumerName);
    }

    private async Task AsegurarGrupoConsumidorAsync(IDatabase db)
    {
        try
        {
            // createStream:true equivale a agregar MKSTREAM: crea el stream si no existe todavía.
            await db.StreamCreateConsumerGroupAsync(
                RedisPedidosConstants.StreamKey,
                RedisPedidosConstants.ConsumerGroup,
                StreamPosition.NewMessages,
                createStream: true);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
            // El grupo ya existía de una ejecución anterior: no es un error real.
            _logger.LogInformation("El consumer group '{Group}' ya existía.", RedisPedidosConstants.ConsumerGroup);
        }
    }

    private async Task ReclamarPendientesAsync(IDatabase db, CancellationToken stoppingToken)
    {
        var pendientes = await db.StreamPendingMessagesAsync(
            RedisPedidosConstants.StreamKey,
            RedisPedidosConstants.ConsumerGroup,
            count: 100,
            consumerName: RedisValue.Null);

        if (pendientes.Length == 0)
        {
            return;
        }

        _logger.LogWarning(
            "Se encontraron {Count} mensaje(s) pendientes de una ejecución anterior. Reclamando...",
            pendientes.Length);

        var idsAReclamar = pendientes.Select(p => p.MessageId).ToArray();

        var reclamados = await db.StreamClaimAsync(
            RedisPedidosConstants.StreamKey,
            RedisPedidosConstants.ConsumerGroup,
            _consumerName,
            minIdleTimeInMs: 0,
            messageIds: idsAReclamar);

        foreach (var entry in reclamados)
        {
            await ProcesarMensajeAsync(db, entry, stoppingToken);
        }
    }

    private async Task ProcesarMensajeAsync(IDatabase db, StreamEntry entry, CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PedidosDbContext>();

        var pedidoIdRaw = entry.Values.FirstOrDefault(v => v.Name == RedisPedidosConstants.FieldPedidoId).Value;

        if (!Guid.TryParse(pedidoIdRaw.ToString(), out var pedidoId))
        {
            _logger.LogError("Mensaje {MessageId} sin pedidoId válido. Se confirma para no bloquear la cola.", entry.Id);
            await db.StreamAcknowledgeAsync(RedisPedidosConstants.StreamKey, RedisPedidosConstants.ConsumerGroup, entry.Id);
            return;
        }

        var pedido = await dbContext.Pedidos.FirstOrDefaultAsync(p => p.Id == pedidoId, stoppingToken);
        if (pedido is null)
        {
            _logger.LogError("Pedido {PedidoId} del mensaje {MessageId} no existe en la base de datos.", pedidoId, entry.Id);
            await db.StreamAcknowledgeAsync(RedisPedidosConstants.StreamKey, RedisPedidosConstants.ConsumerGroup, entry.Id);
            return;
        }

        // Idempotencia básica: si ya fue procesado (por un reclamo duplicado, por ejemplo),
        // no lo procesamos otra vez. Esto es justo lo que las pruebas de duplicación deben verificar.
        if (pedido.Estado is EstadoPedido.Completado or EstadoPedido.Error)
        {
            _logger.LogInformation("Pedido {PedidoId} ya estaba en estado final ({Estado}); se ignora reproceso.", pedido.Id, pedido.Estado);
            await db.StreamAcknowledgeAsync(RedisPedidosConstants.StreamKey, RedisPedidosConstants.ConsumerGroup, entry.Id);
            return;
        }

        pedido.Estado = EstadoPedido.Procesando;
        pedido.FechaActualizacion = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(stoppingToken);

        try
        {
            // Simula tiempo de procesamiento real (validación de stock, cobro, etc.)
            // con algo de variabilidad, útil para observar percentiles bajo carga.
            await Task.Delay(_random.Next(200, 800), stoppingToken);

            // Simula una tasa de error controlada (5%) para tener defectos que
            // el informe de cierre pueda documentar de forma realista.
            if (_random.Next(1, 101) <= 5)
            {
                throw new InvalidOperationException("Fallo simulado de procesamiento (stock insuficiente).");
            }

            pedido.Estado = EstadoPedido.Completado;
            pedido.MensajeError = null;
        }
        catch (Exception ex)
        {
            pedido.Estado = EstadoPedido.Error;
            pedido.MensajeError = ex.Message;
            _logger.LogWarning(ex, "Pedido {PedidoId} terminó en Error.", pedido.Id);
        }

        pedido.FechaActualizacion = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(stoppingToken);

        // Solo se confirma (ACK) después de guardar el resultado final en la base de datos.
        await db.StreamAcknowledgeAsync(RedisPedidosConstants.StreamKey, RedisPedidosConstants.ConsumerGroup, entry.Id);

        _logger.LogInformation("Pedido {PedidoId} procesado -> {Estado}", pedido.Id, pedido.Estado);
    }
}
