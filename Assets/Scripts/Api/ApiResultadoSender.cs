// ApiResultadoSender.cs
using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Envío simple de resultados a tu backend.
/// Configurable por código o vía PlayerPrefs:
///   - PlayerPrefs["api_base_url"]        (p.ej. http://localhost:4000)
///   - PlayerPrefs["api_resultados_path"] (p.ej. /resultados  o  /api/resultados)
///   - PlayerPrefs["auth_token"]          (JWT para Authorization: Bearer ...)
/// </summary>
public static class ApiResultadoSender
{
    // Defaults (puedes sobreescribirlos desde tu GameController)
    public static string BASE_URL = "http://localhost:4000";
    public static string RESULTADOS_PATH = "/resultados";
    public static string AUTH_BEARER = "";   // si está vacío, intenta PlayerPrefs["auth_token"]
    public static float TIMEOUT_SECONDS = 15f;

    [Serializable]
    public class Payload
    {
        // ==== Campos típicos para tu backend ====
        public string alumno_id;        // UUID del alumno
        public string prueba;           // ej. "tol" (planificación)
        public string started_at;       // ISO 8601 (UTC)   e.g. 2025-11-11T21:35:12.345Z
        public string ended_at;         // ISO 8601 (UTC)

        public int aciertos;            // correctos
        public int total_estimulos;     // total objetivo en la ronda
        public int errores_comision;    // intentos/errores por mala categoría, etc.
        public int errores_omision;     // faltantes = total - aciertos

        // ==== Métricas de Tiempo de Reacción (RT) - Coinciden con DB ====
        public int rt_promedio_ms;      // Mean RT
        public int rt_median_ms;        // Median RT (p50)
        public float rt_sd_ms;          // Standard Deviation
        public int rt_min_ms;
        public int rt_max_ms;

        // Campo de texto libre para debug/telemetría adicional (SSRT, SSD, etc. van aquí en JSON)
        public string detalles_raw_text;
    }

    public static IEnumerator PostResultado(Payload payload, Action onOk = null, Action<string> onError = null)
    {
        string baseUrl = PlayerPrefs.GetString("api_base_url", BASE_URL);
        string path    = PlayerPrefs.GetString("api_resultados_path", RESULTADOS_PATH);

        string url = baseUrl.TrimEnd('/') + (path.StartsWith("/") ? path : ("/" + path));

        string json = JsonUtility.ToJson(payload);

        // HACK: Inyección manual de "detalles" como objeto JSON real
        if (!string.IsNullOrEmpty(payload.detalles_raw_text))
        {
            // Encontrar la última llave de cierre '}'
            int lastBrace = json.LastIndexOf('}');
            if (lastBrace > 0)
            {
                string baseJson = json.Substring(0, lastBrace);
                // Añadir el campo detalles. El payload.detalles_raw_text ya es un JSON válido stringificado.
                json = baseJson + $",\"detalles\":{payload.detalles_raw_text}}}";
            }
        }

        Debug.Log($"[ApiResultadoSender] Payload JSON: {json}"); // <-- VERIFICAR ESTO EN CONSOLA

        byte[] body = Encoding.UTF8.GetBytes(json);

        using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.timeout = Mathf.RoundToInt(TIMEOUT_SECONDS);
            req.SetRequestHeader("Content-Type", "application/json");

            // Authorization: Bearer
            string bearer = !string.IsNullOrEmpty(AUTH_BEARER)
                ? AUTH_BEARER
                : (PlayerPrefs.HasKey("auth_token") ? PlayerPrefs.GetString("auth_token") : null);

            if (!string.IsNullOrEmpty(bearer))
                req.SetRequestHeader("Authorization", "Bearer " + bearer);

            yield return req.SendWebRequest();

#if UNITY_2020_3_OR_NEWER
            bool ok = req.result == UnityWebRequest.Result.Success;
#else
            bool ok = !req.isNetworkError && !req.isHttpError;
#endif
            if (ok && req.responseCode >= 200 && req.responseCode < 300)
            {
                Debug.Log("[ApiResultadoSender] OK " + req.responseCode + " → " + url);
                onOk?.Invoke();
            }
            else
            {
                string errorBody = req.downloadHandler != null ? req.downloadHandler.text : "<no body>";
                string err = $"HTTP {(long)req.responseCode} {req.error} \nRespuesta Servidor: {errorBody}";
                Debug.LogWarning("[ApiResultadoSender] ERROR: " + err);
                onError?.Invoke(err);
            }
        }
    }
}
