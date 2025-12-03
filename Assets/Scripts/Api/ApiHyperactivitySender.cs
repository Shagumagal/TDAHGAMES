using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Envía métricas de hiperactividad a la API del backend.
/// Similar a ApiResultadoSender pero específico para métricas de actividad.
/// </summary>
public class ApiHyperactivitySender : MonoBehaviour
{
    // Configuración (usa las mismas que ApiResultadoSender)
    public static string BASE_URL = "http://localhost:4000";
    public static string HYPERACTIVITY_PATH = "/api/metricas-hiperactividad";
    public static string AUTH_BEARER = "";

    [Serializable]
    public class HyperactivityPayload
    {
        public long resultado_id;  // ID del resultado al que pertenece (bigint en PostgreSQL)
        
        // Movimiento del ratón
        public float total_mouse_distance_px;
        public float mean_mouse_speed_px_s;
        public float max_mouse_speed_px_s;
        public float frenetic_movement_rate;
        public int direction_changes;
        
        // Clics
        public int total_clicks;
        public int unnecessary_clicks;
        public float unnecessary_click_rate;
        
        // Teclado
        public int total_key_presses;
        
        // Patrones temporales
        public float burst_activity_rate;
        public float mean_burst_interval_s;
        public float idle_time_ratio;
        public float active_time_ratio;
        
        // Resumen
        public float session_duration_s;
        public float activity_consistency;
    }

    /// <summary>
    /// Envía métricas de hiperactividad a la API
    /// </summary>
    /// <param name="resultadoId">ID del resultado de juego asociado (bigint)</param>
    /// <param name="metrics">Métricas de hiperactividad</param>
    /// <param name="onOk">Callback de éxito</param>
    /// <param name="onError">Callback de error</param>
    public static IEnumerator PostHyperactivityMetrics(
        long resultadoId,
        HyperactivityMetrics metrics,
        Action onOk = null,
        Action<string> onError = null)
    {
        // Convertir HyperactivityMetrics a HyperactivityPayload
        var payload = new HyperactivityPayload
        {
            resultado_id = resultadoId,
            total_mouse_distance_px = metrics.total_mouse_distance_px,
            mean_mouse_speed_px_s = metrics.mean_mouse_speed_px_s,
            max_mouse_speed_px_s = metrics.max_mouse_speed_px_s,
            frenetic_movement_rate = metrics.frenetic_movement_rate,
            direction_changes = metrics.direction_changes,
            total_clicks = metrics.total_clicks,
            unnecessary_clicks = metrics.unnecessary_clicks,
            unnecessary_click_rate = metrics.unnecessary_click_rate,
            total_key_presses = metrics.total_key_presses,
            burst_activity_rate = metrics.burst_activity_rate,
            mean_burst_interval_s = metrics.mean_burst_interval_s,
            idle_time_ratio = metrics.idle_time_ratio,
            active_time_ratio = metrics.active_time_ratio,
            session_duration_s = metrics.session_duration_s,
            activity_consistency = metrics.activity_consistency
        };

        string baseUrl = PlayerPrefs.GetString("api_base_url", BASE_URL);
        string path = PlayerPrefs.GetString("api_hyperactivity_path", HYPERACTIVITY_PATH);
        string url = baseUrl.TrimEnd('/') + (path.StartsWith("/") ? path : ("/" + path));

        string json = JsonUtility.ToJson(payload);
        Debug.Log($"[ApiHyperactivitySender] Enviando métricas: {json}");

        byte[] body = Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            string token = string.IsNullOrEmpty(AUTH_BEARER)
                ? PlayerPrefs.GetString("auth_token", "")
                : AUTH_BEARER;

            if (!string.IsNullOrEmpty(token))
                req.SetRequestHeader("Authorization", $"Bearer {token}");

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("<color=green>[ApiHyperactivitySender] Métricas enviadas OK</color>");
                onOk?.Invoke();
            }
            else
            {
                string err = $"{req.error} | {req.downloadHandler?.text ?? ""}";
                Debug.LogError($"[ApiHyperactivitySender] Error: {err}");
                onError?.Invoke(err);
            }
        }
    }

    /// <summary>
    /// Helper para obtener el último resultado_id guardado
    /// (útil si no tienes acceso directo al ID del resultado)
    /// </summary>
    public static long GetLastResultadoId()
    {
        return long.Parse(PlayerPrefs.GetString("last_resultado_id", "0"));
    }

    /// <summary>
    /// Helper para guardar el último resultado_id
    /// (llama esto desde tus GameControllers después de enviar el resultado)
    /// </summary>
    public static void SaveLastResultadoId(long resultadoId)
    {
        PlayerPrefs.SetString("last_resultado_id", resultadoId.ToString());
        PlayerPrefs.Save();
    }
}
