using Microsoft.EntityFrameworkCore;
using Pedidos.Shared.Data;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);

var postgresConnectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Falta la cadena de conexión 'Postgres' en appsettings.");

var redisConnectionString = builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("Falta la cadena de conexión 'Redis' en appsettings.");

builder.Services.AddDbContext<PedidosDbContext>(options =>
    options.UseNpgsql(postgresConnectionString));

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(redisConnectionString));

builder.Services.AddHostedService<Pedidos.Worker.Worker>();

var host = builder.Build();
host.Run();
