using UnityEngine;

/// <summary>
/// Script de ejemplo que muestra cómo integrar InputActivityTracker en tus juegos.
/// Copia este código en tus GameControllers (SST, GoNoGo, Torre de Londres).
/// </summary>
public class InputActivityTrackerExample : MonoBehaviour
{
    // ===== EJEMPLO 1: Integración básica =====
    
    void OnGameStart()
    {
        // Iniciar tracking al comenzar el juego
        if (InputActivityTracker.Instance != null)
        {
            InputActivityTracker.Instance.StartTracking();
        }
    }

    void OnGameEnd()
    {
        // Obtener métricas al finalizar el juego
        if (InputActivityTracker.Instance != null)
        {
            HyperactivityMetrics metrics = InputActivityTracker.Instance.StopTracking();
            
            // Convertir a JSON para enviar a la API
            string json = JsonUtility.ToJson(metrics);
            Debug.Log($"Métricas de hiperactividad: {json}");
            
            // Aquí puedes agregarlo al payload de tu API
            // payload.detalles_raw_text = json; (o combinarlo con otras métricas)
        }
    }

    // ===== EJEMPLO 2: Registrar zonas válidas para clics =====
    
    void RegisterStimulus(RectTransform stimulusRect)
    {
        // Cuando aparece un estímulo, registra su zona como válida
        if (InputActivityTracker.Instance != null)
        {
            Rect screenRect = GetScreenRect(stimulusRect);
            InputActivityTracker.Instance.RegisterValidClickZone(screenRect);
        }
    }

    void OnStimulusDestroyed()
    {
        // Cuando desaparece el estímulo, limpia las zonas
        if (InputActivityTracker.Instance != null)
        {
            InputActivityTracker.Instance.ClearValidClickZones();
        }
    }

    // ===== EJEMPLO 3: Integración en SST =====
    
    void IntegrateInSST()
    {
        // En SSTSemaforoManager.cs, al inicio de SendResultsToApi:
        
        /*
        HyperactivityMetrics hyperMetrics = null;
        if (InputActivityTracker.Instance != null)
        {
            hyperMetrics = InputActivityTracker.Instance.StopTracking();
        }

        // Luego, al crear el payload, combina las métricas:
        var combinedMetrics = new
        {
            // Métricas SST existentes
            n_trials = nTrials,
            mean_rt = meanRT,
            // ... etc
            
            // Métricas de hiperactividad (si existen)
            hyperactivity = hyperMetrics != null ? new
            {
                mouse_distance = hyperMetrics.total_mouse_distance_px,
                frenetic_rate = hyperMetrics.frenetic_movement_rate,
                unnecessary_clicks = hyperMetrics.unnecessary_click_rate,
                activity_consistency = hyperMetrics.activity_consistency
            } : null
        };

        payload.detalles_raw_text = JsonUtility.ToJson(combinedMetrics);
        */
    }

    // ===== HELPERS =====

    private Rect GetScreenRect(RectTransform rectTransform)
    {
        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);
        
        Vector2 min = RectTransformUtility.WorldToScreenPoint(Camera.main, corners[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(Camera.main, corners[2]);
        
        return new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
    }
}
