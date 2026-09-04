# Pruebas de carga - JMeter

El archivo `pedidos-load-test.jmx` ya viene armado con:
- Un **Thread Group** apuntando a `POST /api/pedidos` (con cliente/cantidad aleatorios por request)
- Un **GET /api/pedidos?estado=Recibido** para simular consulta de pendientes
- **Summary Report** y **Aggregate Report** (de aquí sacas throughput, error % y P95)

## Cómo correrlo

1. Abre JMeter (GUI): `jmeter.sh` (Linux/Mac) o `jmeter.bat` (Windows), o desde línea de comandos.
2. Abre `pedidos-load-test.jmx`.
3. Verifica que la API esté corriendo (`dotnet run` en `Pedidos.Api`) y que las variables `HOST`/`PORT` coincidan (por defecto `localhost:5080`).
4. Ajusta el **Thread Group** ("Escenario") según la tabla obligatoria de la guía:

| Escenario       | Threads (usuarios) | Loop Count | Duración sugerida |
|-----------------|---------------------|------------|--------------------|
| Baseline        | 1                   | 5          | ~1 min             |
| Carga baja      | 10                  | 10         | ~2 min             |
| Carga normal    | 50                  | 15         | ~3 min             |
| Carga alta      | 100                 | 20         | ~3 min             |
| Stress incremental | Incrementar de 100 en 100 | - | Hasta ver degradación |

Para el **Stress Incremental**, la forma más simple es correr el mismo plan subiendo `ThreadGroup.num_threads` en pasos (100 → 200 → 300...) hasta que el error % suba de forma notoria o el tiempo de respuesta se dispare — ese es tu "punto de degradación".

5. Ejecuta (▶) y guarda, para cada escenario, una captura del **Aggregate Report** con: tiempo promedio, throughput, error % y P95. Ponlas en `/evidencias`.

## Qué anotar por cada corrida (va directo a TEST-REPORT.md)

- Tiempo promedio de respuesta
- Throughput (requests/segundo)
- Tasa de error (%)
- Percentil P95
- Cuántos pedidos quedaron en Redis sin procesar todavía (correr `docker exec -it redis-streams redis-cli XLEN pedidos-stream` en la VM)

## Ejecutar sin GUI (opcional, más representativo de un pipeline CI/CD)

```bash
jmeter -n -t pedidos-load-test.jmx -l results/resultado.jtl -e -o results/reporte-html
```
