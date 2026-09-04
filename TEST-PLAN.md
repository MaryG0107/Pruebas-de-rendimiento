# TEST-PLAN — Reto 2: Procesamiento de pedidos mediante cola

## 1. Objetivo y alcance

Validar que el sistema de registro y procesamiento asíncrono de pedidos (API → Redis
Streams → Worker → PostgreSQL) funciona correctamente bajo condiciones normales y de
carga, y que es capaz de tolerar la caída temporal del Worker sin perder ni duplicar
pedidos.

**Dentro del alcance:**
- Validación de reglas de negocio al crear un pedido (pruebas unitarias)
- Interacción real entre API, Redis y Worker (pruebas de integración)
- Comportamiento bajo carga progresiva (JMeter: baseline → stress)
- Resiliencia de la cola ante caída/reinicio del Worker

**Fuera del alcance:**
- Autenticación/autorización de usuarios
- Interfaz gráfica de usuario
- Escalado horizontal con múltiples instancias del Worker (queda como trabajo futuro)

## 2. Arquitectura de la solución

```
Usuario → POST /pedido → Pedidos.Api → Redis Streams → Pedidos.Worker → PostgreSQL
                              ↑                                              │
                              └──────────── GET /pedido/{id} ────────────────┘
```

- **Pedidos.Api**: ASP.NET Core Web API (.NET 8)
- **Pedidos.Worker**: Worker Service (.NET 8), consumer group sobre el stream
- **Redis Streams**: mecanismo de cola/mensajería (contenedor Docker en VM Ubuntu)
- **PostgreSQL**: persistencia de pedidos y su estado

## 3. Riesgos principales

| Riesgo | Impacto | Mitigación / cómo se prueba |
|---|---|---|
| Pérdida de pedidos si el Worker se cae antes de leer el mensaje | Alto | Redis Streams conserva el mensaje hasta ACK; prueba de resiliencia (ver README) |
| Duplicación de procesamiento si dos consumidores leen el mismo mensaje | Medio | Consumer group + verificación de idempotencia en `Worker.cs` |
| Degradación de tiempo de respuesta bajo carga alta | Alto | Progresión JMeter baseline → stress, medir P95 |
| Pedido "atascado" en `Procesando` si el Worker muere a medias | Medio | Revisar `XPENDING` y tiempos de reclamo (`StreamClaim`) |
| Cantidad inválida o negativa aceptada por error | Bajo | Pruebas unitarias de `PedidoValidator` (boundary value analysis) |

## 4. Tipos de prueba y herramientas

| Nivel | Herramienta | Qué valida |
|---|---|---|
| Unitarias | xUnit | Reglas de `PedidoValidator` (Pedidos.Tests.Unit) |
| Integración | xUnit + WebApplicationFactory + Redis + Postgres reales | Flujo API → Redis → Worker → BD (Pedidos.Tests.Integration) |
| Funcional / contratos | Postman | Endpoints y respuestas (`/postman`) |
| Carga / volumen | Apache JMeter | Throughput, error rate, P95 (`/jmeter`) |
| Diagnóstico | Logs de consola + `redis-cli` (XLEN/XPENDING) | Cuellos de botella y mensajes pendientes |

## 5. Casos y datos de prueba

Ver el detalle completo en `tests/Pedidos.Tests.Unit/PedidoValidatorTests.cs` (TC-01 a
TC-07) y `tests/Pedidos.Tests.Integration/PedidoFlowIntegrationTests.cs` (TC-08 a TC-10).
Resumen:

| ID | Caso | Tipo |
|---|---|---|
| TC-01 | Pedido con todos los campos válidos | Unitaria |
| TC-02 | Cliente vacío / solo espacios | Unitaria |
| TC-03 | Producto vacío | Unitaria |
| TC-04 | Cantidad ≤ 0 (boundary value) | Unitaria |
| TC-05 | Cantidad = límite máximo exacto (boundary value) | Unitaria |
| TC-06 | Cantidad = límite máximo + 1 (boundary value) | Unitaria |
| TC-07 | Múltiples campos inválidos a la vez | Unitaria |
| TC-08 | POST persiste y responde 201 | Integración |
| TC-09 | Pedido queda encolado en Redis Streams | Integración |
| TC-10 | Worker procesa el pedido hasta estado final | Integración |

**Pendiente de agregar por el grupo:** casos específicos de la prueba de resiliencia
(detener/reiniciar Worker) con IDs TC-11, TC-12, etc., una vez ejecutados manualmente.

## 6. Escenarios de carga (JMeter)

Ver tabla completa y configuración en `/jmeter/README.md`. Resumen:

| Escenario | Usuarios | Duración sugerida | Objetivo |
|---|---|---|---|
| Baseline | 1 | 1 min | Línea base |
| Carga baja | 10 | 2 min | Estabilidad básica |
| Carga normal | 50 | 3 min | Comportamiento sostenido |
| Carga alta | 100 | 3 min | Detectar degradación |
| Stress incremental | incremental | hasta degradación | Límite operativo |

## 7. Criterios de entrada y salida

**Entrada (para empezar a ejecutar pruebas):**
- Redis y Postgres corriendo y accesibles
- API y Worker compilando y arrancando sin errores
- `dotnet test` de la suite unitaria pasando al 100%

**Salida (para considerar el reto completo):**
- Las 3 pruebas de integración pasan con Redis/Postgres/Worker corriendo
- Los 5 escenarios de JMeter ejecutados con evidencia de métricas (P95, throughput, error %)
- Al menos una corrida documentada de la prueba de resiliencia del Worker
- TEST-REPORT.md completo con conclusión de "listo / no listo para producción"
