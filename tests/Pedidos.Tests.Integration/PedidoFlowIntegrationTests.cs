using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Pedidos.Shared;
using Pedidos.Shared.Models;
using StackExchange.Redis;
using Xunit;

namespace Pedidos.Tests.Integration;

/// <summary>
/// Pruebas de integración TC-08 a TC-10: validan la interacción real entre la API,
/// Redis Streams y la base de datos, y (cuando el Worker está corriendo) el flujo
/// completo hasta el estado final del pedido.
///
/// REQUISITOS PARA CORRER ESTAS PRUEBAS:
///  1. Redis debe estar accesible en localhost:6379 (ver Paso 2 de la guía).
///  2. Postgres debe estar accesible en localhost:5432 con la base 'pedidos_db' creada.
///  3. Para TC-10 (flujo completo), el proyecto Pedidos.Worker debe estar corriendo
///     ("dotnet run" en otra terminal) para que consuma y procese el pedido encolado.
///
/// Esto es intencional: son pruebas de INTEGRACIÓN real entre servicios, no pruebas
/// unitarias con mocks. Si Redis/Postgres no están disponibles, fallarán con un error
/// de conexión claro en vez de un falso positivo.
/// </summary>
public class PedidoFlowIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public PedidoFlowIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact(DisplayName = "TC-08: POST /api/pedidos persiste el pedido y responde 201 con su ubicación")]
    public async Task CrearPedido_ConDatosValidos_Devuelve201YPedidoConsultable()
    {
        var client = _factory.CreateClient();

        var request = new PedidoCreateRequest
        {
            Cliente = "Cliente Integración",
            Producto = "Monitor 24 pulgadas",
            Cantidad = 3
        };

        var response = await client.PostAsJsonAsync("/api/pedidos", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var creado = await response.Content.ReadFromJsonAsync<PedidoResponse>();
        Assert.NotNull(creado);
        Assert.Equal("Recibido", creado!.Estado);

        // Confirma que el pedido realmente quedó persistido y es consultable por id.
        var getResponse = await client.GetAsync($"/api/pedidos/{creado.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Fact(DisplayName = "TC-09: Un pedido creado por la API queda encolado como mensaje en Redis Streams")]
    public async Task CrearPedido_QuedaEncoladoEnRedisStreams()
    {
        var client = _factory.CreateClient();

        var request = new PedidoCreateRequest
        {
            Cliente = "Cliente Redis",
            Producto = "Teclado mecánico",
            Cantidad = 1
        };

        var response = await client.PostAsJsonAsync("/api/pedidos", request);
        response.EnsureSuccessStatusCode();
        var creado = await response.Content.ReadFromJsonAsync<PedidoResponse>();

        await using var redis = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
        var db = redis.GetDatabase();

        // Revisa las últimas entradas del stream buscando el pedidoId recién creado.
        var entries = await db.StreamRangeAsync(RedisPedidosConstants.StreamKey, count: 50, messageOrder: Order.Descending);

        var encontrado = entries.Any(e =>
            e.Values.Any(v => v.Name == RedisPedidosConstants.FieldPedidoId && v.Value == creado!.Id.ToString()));

        Assert.True(encontrado, "El pedido creado no aparece en el stream de Redis.");
    }

    [Fact(DisplayName = "TC-10: Un pedido encolado termina en estado final (Completado o Error) cuando el Worker está corriendo")]
    public async Task CrearPedido_ElWorkerLoProcesaHastaUnEstadoFinal()
    {
        var client = _factory.CreateClient();

        var request = new PedidoCreateRequest
        {
            Cliente = "Cliente Worker",
            Producto = "Mouse inalámbrico",
            Cantidad = 5
        };

        var response = await client.PostAsJsonAsync("/api/pedidos", request);
        response.EnsureSuccessStatusCode();
        var creado = await response.Content.ReadFromJsonAsync<PedidoResponse>();

        // Da tiempo al Worker para consumir y procesar el mensaje (timeout generoso: 10s).
        var timeout = TimeSpan.FromSeconds(10);
        var inicio = DateTime.UtcNow;
        PedidoResponse? estadoActual = null;

        while (DateTime.UtcNow - inicio < timeout)
        {
            var getResponse = await client.GetAsync($"/api/pedidos/{creado!.Id}");
            estadoActual = await getResponse.Content.ReadFromJsonAsync<PedidoResponse>();

            if (estadoActual!.Estado is "Completado" or "Error")
            {
                break;
            }

            await Task.Delay(300);
        }

        Assert.NotNull(estadoActual);
        Assert.True(
            estadoActual!.Estado is "Completado" or "Error",
            $"El pedido se quedó en estado '{estadoActual.Estado}' después de {timeout.TotalSeconds}s. " +
            "¿Está corriendo Pedidos.Worker? Si no, este resultado es esperado y sirve como evidencia " +
            "de mensajes retrasados/pendientes en la cola.");
    }
}
