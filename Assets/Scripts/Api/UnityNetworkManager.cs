using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System;

public class UnityNetworkManager : MonoBehaviour
{
    public static UnityNetworkManager Instance;

    [Header("Configuration")]
    public bool autoLoginOnStart = true;

    // Estado de auth compartido (antes en WebGLGameAuth)
    public static string GameJwt;
    public static string AuthUserId;
    public static string AuthUserNombre;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (autoLoginOnStart)
        {
            StartCoroutine(TryLoginFromUrl());
        }
    }

    private IEnumerator TryLoginFromUrl()
    {
        // 1. Detectar apiUrl personalizada (para WebGL/Vercel)
        string customApiUrl = GetQueryParam("apiUrl");
        if (!string.IsNullOrEmpty(customApiUrl))
        {
            ApiResultadoSender.BASE_URL = customApiUrl;
            ApiHyperactivitySender.BASE_URL = customApiUrl;
            WebGLGameAuth.ApiUrl = customApiUrl;
            Debug.Log($"[UnityNetworkManager] URL actualizada a: {customApiUrl}");
        }

        string baseUrl = ApiResultadoSender.BASE_URL;

        // 2. Intentar obtener token directo de la URL (modo local)
        string token = GetQueryParam("token");
        if (!string.IsNullOrEmpty(token))
        {
            Debug.Log("[UnityNetworkManager] Token directo encontrado en URL");
            SaveAuth(token);
            yield return FetchUserProfile(token, baseUrl);
            yield break;
        }

        // 3. Intentar code exchange (funciona si la URL tiene ?code=...)
        string code = GetQueryParam("code");
        if (!string.IsNullOrEmpty(code))
        {
            Debug.Log($"[UnityNetworkManager] Code encontrado en URL: {code.Substring(0, Math.Min(8, code.Length))}...");
            yield return ExchangeCode(code, baseUrl);
            yield break;
        }

        // 4. FALLBACK para Itch.io: pedir sesión pendiente al servidor
        Debug.Log("[UnityNetworkManager] No code ni token en URL. Intentando claim-pending...");
        yield return ClaimPendingSession(baseUrl);
    }

    private IEnumerator ExchangeCode(string code, string baseUrl)
    {
        string url = $"{baseUrl}/game/exchange?code={UnityWebRequest.EscapeURL(code)}";
        using (var req = UnityWebRequest.Get(url))
        {
            req.timeout = 60;
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("[UnityNetworkManager] Exchange OK (token recibido, no se muestra en el log)");
                ProcessAuthResponse(req.downloadHandler.text);
            }
            else
            {
                Debug.LogWarning($"[UnityNetworkManager] Exchange falló: {req.error}. Intentando claim-pending...");
                yield return ClaimPendingSession(baseUrl);
            }
        }
    }

    private IEnumerator ClaimPendingSession(string baseUrl)
    {
        string url = $"{baseUrl}/game/claim-pending";
        Debug.Log($"[UnityNetworkManager] Llamando a: {url}");

        using (var req = UnityWebRequest.Get(url))
        {
            req.timeout = 60;
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("[UnityNetworkManager] ✅ claim-pending OK (token recibido, no se muestra en el log)");
                ProcessAuthResponse(req.downloadHandler.text);
            }
            else
            {
                Debug.LogWarning($"[UnityNetworkManager] claim-pending falló: {req.responseCode} {req.error}");
                Debug.LogWarning("[UnityNetworkManager] Continuando sin autenticación (usará ID de debug).");
            }
        }
    }

    private void ProcessAuthResponse(string json)
    {
        // Extraer game_jwt
        string tokKey = "\"game_jwt\":\"";
        int tIdx = json.IndexOf(tokKey);
        if (tIdx >= 0)
        {
            int start = tIdx + tokKey.Length;
            int end = json.IndexOf('"', start);
            string jwt = json.Substring(start, end - start);
            GameJwt = jwt;
            SaveAuth(jwt);
            Debug.Log("[UnityNetworkManager] Token JWT guardado OK");
        }

        // Extraer user id
        string idKey = "\"id\":\"";
        int idIdx = json.IndexOf(idKey);
        if (idIdx >= 0)
        {
            int idStart = idIdx + idKey.Length;
            int idEnd = json.IndexOf('"', idStart);
            string parsedId = json.Substring(idStart, idEnd - idStart);

            AuthUserId = parsedId;
            PlayerPrefs.SetString("alumno_id", parsedId);
            PlayerPrefs.Save();
            Debug.Log($"[UnityNetworkManager] ✅ alumno_id REAL guardado: {parsedId}");
        }

        // Extraer nombre
        string nameKey = "\"nombre\":\"";
        int nameIdx = json.IndexOf(nameKey);
        if (nameIdx >= 0)
        {
            int nameStart = nameIdx + nameKey.Length;
            int nameEnd = json.IndexOf('"', nameStart);
            AuthUserNombre = json.Substring(nameStart, nameEnd - nameStart);
            Debug.Log($"[UnityNetworkManager] Usuario: {AuthUserNombre}");
        }
    }

    private void SaveAuth(string token)
    {
        PlayerPrefs.SetString("auth_token", token);
        PlayerPrefs.Save();
        ApiResultadoSender.AUTH_BEARER = token;
        ApiHyperactivitySender.AUTH_BEARER = token;
    }

    private string GetQueryParam(string paramName)
    {
        try
        {
            string url = Application.absoluteURL;
            if (string.IsNullOrEmpty(url)) return null;

            if (url.Contains("?"))
            {
                string queryString = url.Split('?')[1];
                string[] pairs = queryString.Split('&');
                foreach (string pair in pairs)
                {
                    string[] parts = pair.Split('=');
                    if (parts.Length == 2 && parts[0] == paramName)
                    {
                        return Uri.UnescapeDataString(parts[1]);
                    }
                }
            }
            return null;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[UnityNetworkManager] Error parseando URL: {ex.Message}");
            return null;
        }
    }

    [Serializable]
    public class UserProfile
    {
        public string id;
        public string username;
        public string email;
        public string role;
    }

    private IEnumerator FetchUserProfile(string token, string baseUrl)
    {
        string url = $"{baseUrl}/me";

        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.SetRequestHeader("Authorization", $"Bearer {token}");
            req.timeout = 60;
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                string json = req.downloadHandler.text;
                Debug.Log($"[UnityNetworkManager] Perfil cargado: {json}");

                try
                {
                    UserProfile profile = JsonUtility.FromJson<UserProfile>(json);
                    if (profile != null && !string.IsNullOrEmpty(profile.id))
                    {
                        PlayerPrefs.SetString("alumno_id", profile.id);
                        PlayerPrefs.SetString("username", profile.username);
                        PlayerPrefs.Save();
                        Debug.Log($"[UnityNetworkManager] Usuario autenticado: {profile.username} (ID: {profile.id})");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[UnityNetworkManager] Error parseando perfil: {ex.Message}");
                }
            }
            else
            {
                Debug.LogError($"[UnityNetworkManager] Error /me: {req.error} | {req.downloadHandler.text}");
            }
        }
    }
}
