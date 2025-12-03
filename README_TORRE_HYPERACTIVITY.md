# 🎮 Métricas de Hiperactividad - Torre de Londres

## ✅ ¿Qué se ha implementado?

Se ha integrado el sistema de tracking de hiperactividad **solo en el juego de Torre de Londres (Planificación)**.

### Archivos modificados/creados:

1. ✅ **`ApiHyperactivitySender.cs`** - Actualizado para usar `long` (bigint)
2. ✅ **`ApiResultadoSender.cs`** - Extrae y guarda el `resultado_id` automáticamente
3. ✅ **`FarmPackGameController.cs`** - Integrado tracking de hiperactividad
4. ✅ **`TorreHyperactivitySetup.cs`** - Inicializador automático (NUEVO)

---

## 🚀 Cómo activarlo en Unity

### Paso 1: Añadir el script a la escena

1. Abre la escena de **Torre de Londres / Planificación** en Unity
2. Crea un GameObject vacío (clic derecho en Hierarchy → Create Empty)
3. Nómbralo `HyperactivityTracker`
4. Añade el componente `TorreHyperactivitySetup` (arrastra el script o usa Add Component)

**¡Eso es todo!** El tracking se activará automáticamente.

---

## 🔧 Configuración del Backend

Tu backend necesita devolver el `id` del resultado guardado en la respuesta JSON.

### Ejemplo de respuesta esperada:

```json
{
  "message": "Resultado guardado exitosamente",
  "id": 123
}
```

Si tu backend ya devuelve esto, **no necesitas hacer nada más**.

Si NO lo devuelve, modifica tu endpoint `/resultados` para incluir el ID:

```javascript
// En tu backend (Node.js/Express)
const result = await pool.query(query, values);
res.status(201).json({
  message: "Resultado guardado",
  id: result.rows[0].id, // ← IMPORTANTE
});
```

---

## 🧪 Cómo probarlo

1. **Compila Unity** (espera que termine sin errores)
2. **Juega una partida completa** de Torre de Londres
3. **Verifica en la consola de Unity:**

   ```
   [Torre de Londres] Tracking de hiperactividad iniciado
   [API] Resultado enviado correctamente.
   [ApiResultadoSender] resultado_id guardado: 123
   [API] Métricas de hiperactividad enviadas OK
   ```

4. **Verifica en la base de datos:**

   ```sql
   -- Ver el último resultado
   SELECT * FROM app.resultados ORDER BY created_at DESC LIMIT 1;

   -- Ver las métricas de hiperactividad asociadas
   SELECT * FROM app.metricas_hiperactividad ORDER BY created_at DESC LIMIT 1;
   ```

---

## 📊 Métricas que se miden

Durante el juego de Torre de Londres, se rastrean:

### Movimiento del ratón

- Distancia total recorrida (px)
- Velocidad promedio y máxima (px/s)
- Tasa de movimientos "frenéticos"
- Cambios bruscos de dirección

### Clics

- Total de clics
- Clics innecesarios (fuera de objetos válidos)
- Tasa de clics innecesarios

### Teclado

- Total de pulsaciones

### Patrones temporales

- Ráfagas de actividad por segundo
- Intervalo promedio en ráfagas
- Ratio de tiempo idle vs. activo
- Consistencia del ritmo de actividad

---

## 🔍 Consultas SQL útiles

### Ver métricas de hiperactividad con datos del resultado:

```sql
SELECT
    r.id AS resultado_id,
    u.username,
    r.aciertos,
    r.created_at,
    h.total_mouse_distance_px,
    h.frenetic_movement_rate,
    h.unnecessary_click_rate,
    h.activity_consistency
FROM app.resultados r
LEFT JOIN app.usuarios u ON r.alumno_id = u.id
LEFT JOIN app.metricas_hiperactividad h ON r.id = h.resultado_id
WHERE r.prueba_id = (SELECT id FROM app.pruebas WHERE codigo = 'tol')
ORDER BY r.created_at DESC
LIMIT 10;
```

### Ver solo usuarios con alta hiperactividad:

```sql
SELECT
    u.username,
    AVG(h.frenetic_movement_rate) AS movimiento_frenetico_promedio,
    AVG(h.unnecessary_click_rate) AS clics_innecesarios_promedio,
    AVG(h.activity_consistency) AS consistencia_promedio
FROM app.metricas_hiperactividad h
JOIN app.resultados r ON h.resultado_id = r.id
JOIN app.usuarios u ON r.alumno_id = u.id
WHERE r.prueba_id = (SELECT id FROM app.pruebas WHERE codigo = 'tol')
GROUP BY u.username
HAVING AVG(h.frenetic_movement_rate) > 0.3  -- Más del 30% de movimientos frenéticos
ORDER BY movimiento_frenetico_promedio DESC;
```

---

## ⚠️ Troubleshooting

### "No se pudo obtener resultado_id"

- Verifica que tu backend devuelva el `id` en la respuesta JSON
- Revisa la consola del backend para ver qué está devolviendo

### "Métricas no se envían"

- Verifica que el endpoint `/metricas-hiperactividad` esté configurado en tu backend
- Revisa que la tabla `app.metricas_hiperactividad` exista en la base de datos

### "InputActivityTracker.Instance es null"

- Asegúrate de haber añadido el script `TorreHyperactivitySetup` a la escena
- Verifica que no haya errores de compilación en Unity

---

## 🎯 Próximos pasos (opcional)

Si quieres añadir el tracking a los otros juegos (SST, Go/No-Go):

1. Copia el script `TorreHyperactivitySetup.cs` y renómbralo
2. Añádelo a las escenas correspondientes
3. Modifica los GameControllers siguiendo el mismo patrón que en `FarmPackGameController.cs`

---

¿Necesitas ayuda? Revisa los logs de Unity y la base de datos. 🚀
