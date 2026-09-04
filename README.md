# Reto 2 · Procesamiento de pedidos mediante cola

Sistema donde una API .NET recibe pedidos y los coloca en **Redis Streams** para
procesamiento asíncrono por un **Worker Service** independiente, con persistencia en
**PostgreSQL**.

## Arquitectura

```
Usuario
  ↓
POST /api/pedidos
  ↓
Pedidos.Api  ──────►  Redis Streams ("pedidos-stream")
  │                          │
  ▼                          ▼
PostgreSQL  ◄──────  Pedidos.Worker (consumer group "pedidos-workers")
```

- **Pedidos.Api**: expone `POST /api/pedidos`, `GET /api/pedidos/{id}`, `GET /api/pedidos`.
  Valida el pedido, lo persiste con estado `Recibido` y lo publica en el stream.
- **Pedidos.Worker**: `BackgroundService` que consume del stream, marca el pedido como
  `Procesando`, simula el trabajo, y termina en `Completado` o `Error`.
- **Pedidos.Shared**: modelos, DbContext y reglas de validación compartidas por ambos.

## Requisitos previos

- .NET 8 SDK
- Redis corriendo y accesible en `localhost:6379` (contenedor Docker en la VM Ubuntu, puerto reenviado por VirtualBox)
- PostgreSQL corriendo y accesible en `localhost:5432`, con la base `pedidos_db` ya creada

## Cómo correr el proyecto

Desde la raíz del repo:

```bash
# Restaurar dependencias de toda la solución
dotnet restore

# Terminal 1: la API (crea la tabla "pedidos" automáticamente al arrancar en Development)
dotnet run --project src/Pedidos.Api

# Terminal 2: el Worker (consume la cola)
dotnet run --project src/Pedidos.Worker
```

La API queda disponible en `http://localhost:5080` (Swagger en `http://localhost:5080/swagger`).

## Probar manualmente

Con Postman (colección en `/postman/Pedidos.postman_collection.json`) o `curl`:

```bash
curl -X POST http://localhost:5080/api/pedidos \
  -H "Content-Type: application/json" \
  -d '{"cliente":"Mary Sagui","producto":"Laptop Dell","cantidad":2}'
```

Copia el `id` de la respuesta y consulta su estado:

```bash
curl http://localhost:5080/api/pedidos/{id}
```

Si el Worker está corriendo, en 1-2 segundos el estado debería pasar de `Recibido` a
`Procesando` y luego a `Completado` (o `Error`, simulado con 5% de probabilidad a propósito,
para tener defectos reales que documentar).

## Correr las pruebas

```bash
# Unitarias (no requieren Redis ni Postgres)
dotnet test tests/Pedidos.Tests.Unit

# Integración (requieren Redis y Postgres corriendo; para TC-10 también el Worker)
dotnet test tests/Pedidos.Tests.Integration
```

## Probar la resiliencia del Worker (obligatorio en la guía)

1. Con la API corriendo, crea 5-10 pedidos (vía Postman o un loop de `curl`).
2. **Detén el Worker** (Ctrl+C en su terminal).
3. Verifica que los mensajes quedan pendientes en el stream:
   ```bash
   ssh mary@localhost -p 2222
   docker exec -it redis-streams redis-cli XLEN pedidos-stream
   docker exec -it redis-streams redis-cli XPENDING pedidos-stream pedidos-workers
   ```
4. Reinicia el Worker (`dotnet run --project src/Pedidos.Worker`) y mide cuánto tarda en
   drenar la cola (el Worker reclama automáticamente los mensajes pendientes al arrancar).
5. Verifica en la base de datos que ningún pedido se procesó dos veces (columna `Estado`
   no debería regresar de `Completado`/`Error` a `Procesando`).

## Estructura del repositorio

```
/proyecto
  /src
    /Pedidos.Api          Web API
    /Pedidos.Worker       Worker Service (consumidor de Redis Streams)
    /Pedidos.Shared       Modelos, DbContext y validaciones compartidas
  /tests
    /Pedidos.Tests.Unit          Pruebas unitarias de reglas de negocio
    /Pedidos.Tests.Integration   Pruebas de integración API+Redis+Worker+BD
  /jmeter      Plan de carga y guía de escenarios
  /postman     Colección de contratos de la API
  /evidencias  Capturas y resultados de las corridas (agregar aquí)
  TEST-PLAN.md
  TEST-REPORT.md
```

## Nota sobre `appsettings.Development.json`

Este archivo contiene la contraseña real de tu Postgres local y está excluido en
`.gitignore` — **no lo subas al repositorio**. Cada integrante del grupo que clone el
repo debe crear su propio `appsettings.Development.json` a partir del `appsettings.json`
base, con sus propias credenciales locales.
