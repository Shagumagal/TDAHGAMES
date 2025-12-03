# 📊 Sistema de Métricas de Hiperactividad - Guía de Implementación

## 🎯 Objetivo

Medir indicadores digitales de hiperactividad e impulsividad (criterios DSM-5) mediante el tracking de actividad de entrada (ratón, teclado, clics) durante los juegos.

---

## 📦 Archivos creados

### Unity (C#)

1. `InputActivityTracker.cs` - Tracker principal
2. `InputActivityTrackerInitializer.cs` - Inicializador automático
3. `InputActivityTrackerExample.cs` - Ejemplos de uso
4. `ApiHyperactivitySender.cs` - Envío a la API

### Backend

5. `database_migration_hyperactivity.sql` - Migración de BD
6. `backend_endpoint_hyperactivity.js` - Endpoint Node.js

---

## 🚀 Pasos de implementación

### 1️⃣ Base de Datos (PostgreSQL)

```bash
# Ejecuta la migración en tu base de datos
psql -U tu_usuario -d tu_base_de_datos -f database_migration_hyperactivity.sql
```

Esto creará la tabla `app.metricas_hiperactividad` con todos los campos necesarios.

---

### 2️⃣ Backend (Node.js/Express)

1. Copia `backend_endpoint_hyperactivity.js` a tu carpeta `routes/`
2. En tu `app.js` o `index.js`, agrega:

```javascript
const metricasHiperactividadRouter = require("./routes/metricas-hiperactividad");
app.use("/api", metricasHiperactividadRouter);
```

3. Reinicia tu servidor backend

---

### 3️⃣ Unity - Opción A: Automática (Sin modificar código)

1. En cada escena de juego (SST, GoNoGo, Torre):

   - Crea un GameObject vacío llamado "HyperactivityTracker"
   - Añade el componente `InputActivityTrackerInitializer`
   - Marca `Auto Start` en el Inspector

2. **Limitación**: Las métricas se medirán pero NO se enviarán automáticamente a la API.

---

### 3️⃣ Unity - Opción B: Integrada (Recomendada)

Necesitas modificar tus GameControllers para enviar las métricas. Aquí te muestro cómo:

#### Para SST (`SSTSemaforoManager.cs`)

```csharp
// Al INICIO del juego (en tu método de inicio):
void IniciarJuego()
{
    if (InputActivityTracker.Instance != null)
    {
        InputActivityTracker.Instance.StartTracking();
    }
    // ... resto de tu código
}

// Al FINAL del juego (después de enviar el resultado):
IEnumerator SendResultsToApi()
{
    // ... tu código existente para enviar el resultado del juego ...

    yield return ApiResultadoSender.PostResultado(payload,
        onOk: () => {
            Debug.Log("Resultado enviado OK");

            // NUEVO: Obtener y enviar métricas de hiperactividad
            if (InputActivityTracker.Instance != null)
            {
                HyperactivityMetrics hyperMetrics = InputActivityTracker.Instance.StopTracking();

                // Necesitas el ID del resultado que acabas de guardar
                // Opción 1: Si tu backend devuelve el ID en la respuesta
                string resultadoId = "UUID-del-resultado"; // Obtener del response

                // Opción 2: Si usas un UUID generado en Unity
                // string resultadoId = System.Guid.NewGuid().ToString();

                StartCoroutine(ApiHyperactivitySender.PostHyperactivityMetrics(
                    resultadoId,
                    hyperMetrics,
                    onOk: () => Debug.Log("Métricas de hiperactividad enviadas OK"),
                    onError: (err) => Debug.LogError($"Error enviando métricas: {err}")
                ));
            }
        },
        onError: (err) => Debug.LogError($"Error: {err}")
    );
}
```

---

## 📊 Métricas que se miden

### Movimiento del ratón

- `total_mouse_distance_px`: Distancia total recorrida
- `mean_mouse_speed_px_s`: Velocidad promedio
- `max_mouse_speed_px_s`: Velocidad máxima
- `frenetic_movement_rate`: % de movimientos "frenéticos"
- `direction_changes`: Cambios bruscos de dirección

### Clics

- `total_clicks`: Total de clics
- `unnecessary_clicks`: Clics fuera de zonas válidas
- `unnecessary_click_rate`: % de clics innecesarios

### Teclado

- `total_key_presses`: Total de pulsaciones

### Patrones temporales

- `burst_activity_rate`: Ráfagas de actividad por segundo
- `mean_burst_interval_s`: Intervalo promedio en ráfagas
- `idle_time_ratio`: % de tiempo sin actividad
- `active_time_ratio`: % de tiempo activo

### Resumen

- `session_duration_s`: Duración de la sesión
- `activity_consistency`: Consistencia del ritmo (0-1)

---

## 🔍 Consultas útiles

### Ver métricas de un resultado específico

```sql
SELECT r.prueba, r.aciertos, h.*
FROM app.resultados r
LEFT JOIN app.metricas_hiperactividad h ON r.id = h.resultado_id
WHERE r.id = 'UUID-del-resultado';
```

### Ver todas las métricas de un alumno

```sql
SELECT r.prueba, r.created_at, h.frenetic_movement_rate, h.unnecessary_click_rate
FROM app.resultados r
LEFT JOIN app.metricas_hiperactividad h ON r.id = h.resultado_id
WHERE r.alumno_id = 'UUID-del-alumno'
ORDER BY r.created_at DESC;
```

---

## ⚠️ Importante: Obtener el resultado_id

Para vincular las métricas de hiperactividad con el resultado del juego, necesitas el `id` del resultado que acabas de guardar.

### Opción 1: Modificar tu backend para que devuelva el ID

En tu endpoint `/resultados` (backend), modifica la respuesta para incluir el ID:

```javascript
// Después de insertar el resultado
const result = await pool.query(query, values);
res.status(201).json({
  message: "Resultado guardado",
  id: result.rows[0].id, // ← IMPORTANTE: Devolver el ID
});
```

Luego en Unity, captura ese ID:

```csharp
// En ApiResultadoSender.cs, modifica PostResultado para capturar el ID
// y guardarlo en PlayerPrefs o devolverlo via callback
```

### Opción 2: Generar UUID en Unity

```csharp
// Al crear el payload del resultado, genera un UUID
string resultadoId = System.Guid.NewGuid().ToString();

var payload = new ApiResultadoSender.Payload
{
    id = resultadoId,  // Agregar este campo
    alumno_id = ...,
    // ... resto de campos
};
```

---

## 🧪 Testing

1. Juega una partida completa de SST/GoNoGo/Torre
2. Verifica en la consola de Unity:
   - `[InputActivityTracker] Tracking iniciado`
   - `[ApiHyperactivitySender] Métricas enviadas OK`
3. Verifica en la base de datos:
   ```sql
   SELECT * FROM app.metricas_hiperactividad ORDER BY created_at DESC LIMIT 1;
   ```

---

## 📈 Próximos pasos

Una vez implementado, podrás:

1. Analizar patrones de hiperactividad por alumno
2. Comparar métricas entre diferentes juegos
3. Usar estas métricas como features adicionales para tu modelo de ML
4. Generar reportes visuales de actividad

---

## 🆘 Troubleshooting

### "Métricas no se envían"

- Verifica que `InputActivityTracker.Instance` no sea null
- Asegúrate de llamar `StartTracking()` al inicio del juego
- Verifica que el endpoint del backend esté correcto

### "Error 404 en el backend"

- Verifica que hayas agregado el router en tu `app.js`
- Verifica que la ruta sea `/api/metricas-hiperactividad`

### "Error: resultado_id no encontrado"

- Asegúrate de enviar primero el resultado del juego
- Verifica que el `resultado_id` sea correcto

---

¿Necesitas ayuda con algún paso específico? 🚀
