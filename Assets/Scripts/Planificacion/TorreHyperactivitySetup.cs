using UnityEngine;

/// <summary>
/// Inicializa automáticamente el InputActivityTracker para la escena de Torre de Londres.
/// Añade este script a un GameObject vacío en la escena de Planificación.
/// </summary>
[DefaultExecutionOrder(-100)] // Ejecutar antes que otros scripts
public class TorreHyperactivitySetup : MonoBehaviour
{
    void Awake()
    {
        // Crear el tracker si no existe
        if (InputActivityTracker.Instance == null)
        {
            GameObject trackerGO = new GameObject("InputActivityTracker");
            var tracker = trackerGO.AddComponent<InputActivityTracker>();
            
            // Configuración específica para Torre de Londres
            tracker.freneticSpeedThreshold = 500f;
            tracker.idleThreshold = 2f;
            
            Debug.Log("[Torre de Londres] InputActivityTracker creado automáticamente");
        }
    }
}
