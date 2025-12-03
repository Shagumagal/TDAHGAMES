using UnityEngine;

/// <summary>
/// Inicializador automático del InputActivityTracker.
/// Añade este script a un GameObject vacío en cada escena de juego.
/// </summary>
public class InputActivityTrackerInitializer : MonoBehaviour
{
    [Header("Configuración")]
    [Tooltip("Iniciar tracking automáticamente al cargar la escena")]
    public bool autoStart = true;

    [Tooltip("Umbral de velocidad del ratón (px/s) para considerar movimiento 'frenético'")]
    public float freneticSpeedThreshold = 500f;

    [Tooltip("Tiempo mínimo sin input (segundos) para considerar 'pausa'")]
    public float idleThreshold = 2f;

    void Start()
    {
        // Crear el tracker si no existe
        if (InputActivityTracker.Instance == null)
        {
            GameObject trackerGO = new GameObject("InputActivityTracker");
            var tracker = trackerGO.AddComponent<InputActivityTracker>();
            tracker.freneticSpeedThreshold = freneticSpeedThreshold;
            tracker.idleThreshold = idleThreshold;
        }

        // Iniciar tracking si está configurado
        if (autoStart && InputActivityTracker.Instance != null)
        {
            InputActivityTracker.Instance.StartTracking();
            Debug.Log("[InputActivityTracker] Tracking iniciado automáticamente");
        }
    }
}
