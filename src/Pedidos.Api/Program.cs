using Microsoft.EntityFrameworkCore;
using Pedidos.Api.Services;
using Pedidos.Shared.Data;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// --- Servicios ---

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var postgresConnectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Falta la cadena de conexión 'Postgres' en appsettings.");

builder.Services.AddDbContext<PedidosDbContext>(options =>
    options.UseNpgsql(postgresConnectionString));

var redisConnectionString = builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("Falta la cadena de conexión 'Redis' en appsettings.");

// Un solo ConnectionMultiplexer compartido para toda la app (patrón recomendado por StackExchange.Redis).
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(redisConnectionString));

builder.Services.AddScoped<IPedidoQueuePublisher, RedisPedidoQueuePublisher>();

var app = builder.Build();

// --- Creación automática del esquema en desarrollo ---
// Se usa EnsureCreated() (en vez de migraciones) para simplificar el arranque local:
// crea la tabla "pedidos" en pedidos_db si no existe todavía. Si más adelante el
// grupo quiere versionar el esquema con migraciones formales, pueden reemplazar esto
// por "dotnet ef migrations add Inicial" + db.Database.Migrate().
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<PedidosDbContext>();
    db.Database.EnsureCreated();
}

// --- Middleware pipeline ---

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();

// Endpoint simple de salud, útil como baseline en JMeter antes de cargar /api/pedidos.
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

// Necesario para que WebApplicationFactory<Program> funcione en las pruebas de integración.
public partial class Program { }
