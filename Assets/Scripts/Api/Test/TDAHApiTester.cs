using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;

public class TDAHApiTester : MonoBehaviour
{
    // En el Editor usa tu API local, en build usa Render
#if UNITY_EDITOR
    public string apiBaseUrl = "http://localhost:4000";
#else
    public string apiBaseUrl = "https://proyectotdah.onrender.com";
#endif

    [Header("Configuración de Prueba")]
    [Tooltip("Copia aquí un UUID válido de tu base de datos (tabla app.usuarios o app.alumnos)")]
    public string testAlumnoUUID = "00000000-0000-0000-0000-000000000000";

    [Header("Códigos de Pruebas (Ver tabla app.pruebas)")]
    public string codeSST = "sst";       // Quizás sea "SST" o "STOP"
    public string codeGoNoGo = "gonogo"; // Quizás sea "GONOGO"
    public string codePlanning = "tol";  // Quizás sea "TOL" o "PLANIFICACION"

    [System.Serializable]
    public class LoginPasswordBody
    {
        public string identifier;   // correo o username
        public string password;
    }

    [System.Serializable]
    public class LoginResponse
    {
        public string status;
        public string token;
        public string message;
        // Agrega más campos si tu API devuelve user, etc.
    }

    // Botón en el menú contextual del componente
    [ContextMenu("1. Login (Guardar Token)")]
    public void TestLogin()
    {
        StartCoroutine(LoginCoroutine());
    }

    [ContextMenu("2. Enviar SST (Simulado)")]
    public void TestSendSST()
    {
        StartCoroutine(SendSSTCoroutine());
    }

    [ContextMenu("3. Enviar GoNoGo (Simulado)")]
    public void TestSendGoNoGo()
    {
        StartCoroutine(SendGoNoGoCoroutine());
    }

    [ContextMenu("4. Enviar Planificación (Simulado)")]
    public void TestSendPlanning()
    {
        StartCoroutine(SendPlanningCoroutine());
    }

    private IEnumerator LoginCoroutine()
    {
        string url = apiBaseUrl.TrimEnd('/') + "/auth/login-password";

        var body = new LoginPasswordBody
        {
            identifier = "alumno_123",     // Asegúrate que este usuario exista en tu DB local
            password   = "123456"
        };

        string json = JsonUtility.ToJson(body);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        using (var request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler   = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");

            Debug.Log($"[TDAHApiTester] POST {url} body={json}");

            yield return request.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            bool ok = request.result == UnityWebRequest.Result.Success;
#else
            bool ok = !request.isNetworkError && !request.isHttpError;
#endif

            if (!ok)
            {
                Debug.LogError($"[TDAHApiTester] ERROR {request.responseCode} - {request.error}\n{request.downloadHandler.text}");
            }
            else
            {
                string respJson = request.downloadHandler.text;
                Debug.Log($"[TDAHApiTester] OK: {respJson}");
                
                // Intentar guardar el token
                try
                {
                    var resp = JsonUtility.FromJson<LoginResponse>(respJson);
                    if (!string.IsNullOrEmpty(resp.token))
                    {
                        PlayerPrefs.SetString("auth_token", resp.token);
                        PlayerPrefs.SetString("api_base_url", apiBaseUrl); // Configurar base url también
                        PlayerPrefs.Save();
                        Debug.Log("<color=green>[TDAHApiTester] Token guardado en PlayerPrefs!</color>");
                    }
                }
                catch (System.Exception e) { Debug.LogWarning("No se pudo parsear token: " + e.Message); }
            }
        }
    }

    private IEnumerator SendSSTCoroutine()
    {
        var payload = new ApiResultadoSender.Payload
        {
            alumno_id = testAlumnoUUID, // UUID real
            prueba = codeSST,
            started_at = System.DateTime.UtcNow.AddMinutes(-5).ToString("o"),
            ended_at = System.DateTime.UtcNow.ToString("o"),
            aciertos = 45,
            total_estimulos = 50,
            errores_comision = 3, // Falló en frenar
            errores_omision = 2,  // No respondió al Go
            
            rt_promedio_ms = 450,
            rt_median_ms = 440,
            rt_sd_ms = 55.5f,
            rt_min_ms = 300,
            rt_max_ms = 600,

            detalles_raw_text = JsonUtility.ToJson(new {
                ssrt_ms = 275,
                ssd_mean_ms = 200,
                stop_success_rate = 0.55f,
                rt_go_cv = 0.18f
            })
        };

        Debug.Log("[TDAHApiTester] Enviando SST...");
        yield return ApiResultadoSender.PostResultado(payload, 
            () => Debug.Log("<color=green>SST Enviado OK</color>"), 
            (e) => Debug.LogError("SST Error: " + e));
    }

    private IEnumerator SendGoNoGoCoroutine()
    {
        var payload = new ApiResultadoSender.Payload
        {
            alumno_id = testAlumnoUUID,
            prueba = codeGoNoGo,
            started_at = System.DateTime.UtcNow.AddMinutes(-4).ToString("o"),
            ended_at = System.DateTime.UtcNow.ToString("o"),
            aciertos = 90,
            total_estimulos = 100,
            errores_comision = 8, // Apretó en NoGo
            errores_omision = 2,  // No apretó en Go
            
            rt_promedio_ms = 380,
            rt_median_ms = 375,
            rt_sd_ms = 40.2f,
            rt_min_ms = 250,
            rt_max_ms = 500,

            detalles_raw_text = JsonUtility.ToJson(new {
                rt_cv = 0.15f,
                fast_guess_rate = 0.02f,
                vigilance_decrement = 0.05f
            })
        };

        Debug.Log("[TDAHApiTester] Enviando GoNoGo...");
        yield return ApiResultadoSender.PostResultado(payload, 
            () => Debug.Log("<color=green>GoNoGo Enviado OK</color>"), 
            (e) => Debug.LogError("GoNoGo Error: " + e));
    }

    private IEnumerator SendPlanningCoroutine()
    {
        var payload = new ApiResultadoSender.Payload
        {
            alumno_id = testAlumnoUUID,
            prueba = codePlanning, // Tower of London / Planning
            started_at = System.DateTime.UtcNow.AddMinutes(-10).ToString("o"),
            ended_at = System.DateTime.UtcNow.ToString("o"),
            aciertos = 5,
            total_estimulos = 5,
            errores_comision = 0,
            errores_omision = 0,
            
            // En planificación el RT rápido no importa tanto, van en 0
            rt_promedio_ms = 0,
            rt_median_ms = 0,
            rt_sd_ms = 0,

            detalles_raw_text = JsonUtility.ToJson(new {
                first_action_latency_s = 4.5f,
                mean_decision_time_s = 2.3f,
                sequence_compliance = 1.0f,
                excess_moves = 2
            })
        };

        Debug.Log("[TDAHApiTester] Enviando Planificación...");
        yield return ApiResultadoSender.PostResultado(payload, 
            () => Debug.Log("<color=green>Planificación Enviado OK</color>"), 
            (e) => Debug.LogError("Planificación Error: " + e));
    }
}
