using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Rastrea la actividad de entrada (ratón, teclado, clics) para medir indicadores
/// de hiperactividad e impulsividad según criterios DSM-5.
/// </summary>
public class InputActivityTracker : MonoBehaviour
{
    [Header("Configuración")]
    [Tooltip("Umbral de velocidad del ratón (px/s) para considerar movimiento 'frenético'")]
    public float freneticSpeedThreshold = 500f;

    [Tooltip("Tiempo mínimo sin input (segundos) para considerar 'pausa'")]
    public float idleThreshold = 2f;

    [Tooltip("Radio (px) alrededor de estímulos válidos para ignorar clics")]
    public float validClickRadius = 50f;

    // Singleton para acceso global
    public static InputActivityTracker Instance { get; private set; }

    // Métricas acumuladas
    private float totalMouseDistance = 0f;
    private List<float> mouseSpeeds = new List<float>();
    private int totalClicks = 0;
    private int unnecessaryClicks = 0;
    private int totalKeyPresses = 0;
    private List<float> burstIntervals = new List<float>(); // Intervalos entre acciones en ráfagas
    private float lastInputTime = 0f;
    private float totalIdleTime = 0f;
    private float totalActiveTime = 0f;
    private int directionChanges = 0;

    // Estado interno
    private Vector3 lastMousePos;
    private Vector2 lastMouseDirection;
    private float sessionStartTime;
    private bool isTracking = false;

    // Zonas válidas para clics (para detectar clics innecesarios)
    private List<Rect> validClickZones = new List<Rect>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        lastMousePos = Input.mousePosition;
        sessionStartTime = Time.time;
    }

    void Update()
    {
        if (!isTracking) return;

        float deltaTime = Time.deltaTime;
        float currentTime = Time.time;

        // ===== TRACKING DE RATÓN =====
        Vector3 currentMousePos = Input.mousePosition;
        float distance = Vector3.Distance(currentMousePos, lastMousePos);
        
        if (distance > 0.1f) // Ignorar micro-movimientos
        {
            totalMouseDistance += distance;
            float speed = distance / deltaTime;
            mouseSpeeds.Add(speed);

            // Detectar cambios de dirección
            Vector2 currentDirection = (currentMousePos - lastMousePos).normalized;
            if (lastMouseDirection != Vector2.zero)
            {
                float angle = Vector2.Angle(lastMouseDirection, currentDirection);
                if (angle > 90f) // Cambio brusco de dirección
                {
                    directionChanges++;
                }
            }
            lastMouseDirection = currentDirection;

            RecordInput(currentTime);
        }

        lastMousePos = currentMousePos;

        // ===== TRACKING DE CLICS =====
        if (Input.GetMouseButtonDown(0))
        {
            totalClicks++;
            
            // Verificar si el clic fue en una zona válida
            bool isValidClick = validClickZones.Any(zone => zone.Contains(Input.mousePosition));
            if (!isValidClick)
            {
                unnecessaryClicks++;
            }

            RecordInput(currentTime);
        }

        // ===== TRACKING DE TECLADO =====
        if (Input.anyKeyDown)
        {
            totalKeyPresses++;
            RecordInput(currentTime);
        }

        // ===== TRACKING DE IDLE TIME =====
        float timeSinceLastInput = currentTime - lastInputTime;
        if (timeSinceLastInput > idleThreshold)
        {
            totalIdleTime += deltaTime;
        }
        else
        {
            totalActiveTime += deltaTime;
        }
    }

    private void RecordInput(float currentTime)
    {
        float interval = currentTime - lastInputTime;
        
        // Si el intervalo es muy corto, es parte de una ráfaga
        if (interval < 0.5f && interval > 0.01f)
        {
            burstIntervals.Add(interval);
        }

        lastInputTime = currentTime;
    }

    // ===== API PÚBLICA =====

    /// <summary>
    /// Inicia el tracking de actividad
    /// </summary>
    public void StartTracking()
    {
        isTracking = true;
        sessionStartTime = Time.time;
        lastInputTime = Time.time;
        ResetMetrics();
    }

    /// <summary>
    /// Detiene el tracking y devuelve las métricas
    /// </summary>
    public HyperactivityMetrics StopTracking()
    {
        isTracking = false;
        return CalculateMetrics();
    }

    /// <summary>
    /// Registra una zona válida para clics (ej. botones, estímulos)
    /// </summary>
    public void RegisterValidClickZone(Rect zone)
    {
        validClickZones.Add(zone);
    }

    /// <summary>
    /// Limpia todas las zonas válidas
    /// </summary>
    public void ClearValidClickZones()
    {
        validClickZones.Clear();
    }

    /// <summary>
    /// Resetea todas las métricas
    /// </summary>
    public void ResetMetrics()
    {
        totalMouseDistance = 0f;
        mouseSpeeds.Clear();
        totalClicks = 0;
        unnecessaryClicks = 0;
        totalKeyPresses = 0;
        burstIntervals.Clear();
        totalIdleTime = 0f;
        totalActiveTime = 0f;
        directionChanges = 0;
        validClickZones.Clear();
        lastMouseDirection = Vector2.zero;
    }

    // ===== CÁLCULO DE MÉTRICAS =====

    private HyperactivityMetrics CalculateMetrics()
    {
        float sessionDuration = Time.time - sessionStartTime;
        
        var metrics = new HyperactivityMetrics
        {
            // Movimiento del ratón
            total_mouse_distance_px = totalMouseDistance,
            mean_mouse_speed_px_s = mouseSpeeds.Count > 0 ? mouseSpeeds.Average() : 0f,
            max_mouse_speed_px_s = mouseSpeeds.Count > 0 ? mouseSpeeds.Max() : 0f,
            frenetic_movement_rate = mouseSpeeds.Count > 0 
                ? mouseSpeeds.Count(s => s > freneticSpeedThreshold) / (float)mouseSpeeds.Count 
                : 0f,
            direction_changes = directionChanges,
            
            // Clics
            total_clicks = totalClicks,
            unnecessary_clicks = unnecessaryClicks,
            unnecessary_click_rate = totalClicks > 0 ? unnecessaryClicks / (float)totalClicks : 0f,
            
            // Teclado
            total_key_presses = totalKeyPresses,
            
            // Patrones temporales
            burst_activity_rate = burstIntervals.Count / Mathf.Max(1f, sessionDuration),
            mean_burst_interval_s = burstIntervals.Count > 0 ? burstIntervals.Average() : 0f,
            idle_time_ratio = sessionDuration > 0 ? totalIdleTime / sessionDuration : 0f,
            active_time_ratio = sessionDuration > 0 ? totalActiveTime / sessionDuration : 0f,
            
            // Resumen
            session_duration_s = sessionDuration,
            activity_consistency = CalculateConsistency()
        };

        return metrics;
    }

    private float CalculateConsistency()
    {
        // Mide qué tan consistente es el ritmo de actividad
        // Valores bajos = actividad errática (hiperactividad)
        // Valores altos = actividad consistente
        
        if (burstIntervals.Count < 2) return 1f;

        float mean = burstIntervals.Average();
        float variance = burstIntervals.Sum(x => Mathf.Pow(x - mean, 2)) / burstIntervals.Count;
        float stdDev = Mathf.Sqrt(variance);
        
        // Coeficiente de variación invertido (0 = muy inconsistente, 1 = muy consistente)
        float cv = mean > 0 ? stdDev / mean : 0f;
        return Mathf.Clamp01(1f - cv);
    }
}

/// <summary>
/// Métricas de hiperactividad/impulsividad basadas en actividad de entrada
/// </summary>
[Serializable]
public class HyperactivityMetrics
{
    // Movimiento del ratón
    public float total_mouse_distance_px;
    public float mean_mouse_speed_px_s;
    public float max_mouse_speed_px_s;
    public float frenetic_movement_rate;  // % de movimientos "frenéticos"
    public int direction_changes;         // Cambios bruscos de dirección

    // Clics
    public int total_clicks;
    public int unnecessary_clicks;        // Clics fuera de zonas válidas
    public float unnecessary_click_rate;  // % de clics innecesarios

    // Teclado
    public int total_key_presses;

    // Patrones temporales
    public float burst_activity_rate;     // Ráfagas de actividad por segundo
    public float mean_burst_interval_s;   // Intervalo promedio en ráfagas
    public float idle_time_ratio;         // % de tiempo sin hacer nada
    public float active_time_ratio;       // % de tiempo activo

    // Resumen
    public float session_duration_s;
    public float activity_consistency;    // 0-1, qué tan consistente es el ritmo
}
