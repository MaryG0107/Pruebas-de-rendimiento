# TEST-REPORT — Reto 2: Procesamiento de pedidos mediante cola

> ⚠️ Este documento es una plantilla. Complétalo con los resultados reales después de
> correr las pruebas y JMeter — no lo entregues así.

## 1. Resumen de ejecución

| Suite | Passed | Failed | Blocked | Total |
|---|---|---|---|---|
| Pedidos.Tests.Unit | | | | 7 |
| Pedidos.Tests.Integration | | | | 3 |

## 2. Métricas de carga y concurrencia

| Escenario | Usuarios | Tiempo promedio (ms) | Throughput (req/s) | Error rate (%) | P95 (ms) |
|---|---|---|---|---|---|
| Baseline | 1 | | | | |
| Carga baja | 10 | | | | |
| Carga normal | 50 | | | | |
| Carga alta | 100 | | | | |
| Stress incremental | | | | | |

**Punto de degradación observado:** _(a partir de cuántos usuarios concurrentes empezó a subir el error rate o el tiempo de respuesta de forma notoria)_

## 3. Resultados de la prueba de resiliencia del Worker

- Pedidos creados antes de detener el Worker: ___
- Mensajes pendientes en el stream tras detenerlo (`XPENDING`): ___
- Tiempo de drenado tras reiniciar el Worker: ___
- ¿Se perdió algún pedido? ___
- ¿Se duplicó el procesamiento de algún pedido? ___

## 4. Defectos encontrados

| ID | Descripción | Severidad | Estado |
|---|---|---|---|
| DEF-01 | | | |

## 5. Cuello de botella principal

_(Ejemplo de preguntas guía: ¿fue la API, Redis, el Worker, o la base de datos? ¿Fue CPU, memoria, o número de conexiones?)_

## 6. Conclusiones y recomendaciones

## 7. Decisión final

**¿Listo para producción?** Sí / No

**Justificación:**
