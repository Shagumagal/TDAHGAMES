using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System;

public class UnityNetworkManager : MonoBehaviour
{
    public static UnityNetworkManager Instance;

    [Header("Configuration")]
    public bool autoLoginOnStart = true;

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
        // 1. Intentar obtener la URL de la API de los parámetros (para WebGL/Vercel)
        string customApiUrl = GetQueryParam("apiUrl");
        if (!string.IsNullOrEmpty(customApiUrl))
        {
            // Esto asegura que el juego use la URL de Render cuando esté en la web
            ApiResultadoSender.BASE_URL = customApiUrl;
            ApiHyperactivitySender.BASE_URL = customApiUrl; 
            Debug.Log($"[UnityNetworkManager] URL actualizada dinámicamente a: {customApiUrl}");
        }

        // 2. Intentar obtener el token de la URL
        string token = GetQueryParam("token");
        
        // Si no hay token en la URL, verificar si tenemos uno guardado de una sesión anterior
        if (string.IsNullOrEmpty(token))
        {
            token = PlayerPrefs.GetString("auth_token", "");
        }

        if (!string.IsNullOrEmpty(token))
        {
            Debug.Log($"[UnityNetworkManager] Token detectado: {token.Substring(0, Math.Min(10, token.Length))}...");
            
            // Guardar token para uso global
            PlayerPrefs.SetString("auth_token", token);
            PlayerPrefs.Save();
            
            // Actualizar referencias estáticas en los senders
            ApiResultadoSender.AUTH_BEARER = token;
            ApiHyperactivitySender.AUTH_BEARER = token;

            // 2. Obtener perfil del usuario para saber su ID (alumno_id)
            yield return FetchUserProfile(token);
        }
        else
        {
            Debug.Log("[UnityNetworkManager] No se encontró token. Modo Offline o Desarrollo.");
        }
    }

    private string GetQueryParam(string paramName)
    {
        try
        {
            string url = Application.absoluteURL;
            // Descomentar para probar en editor simulando una URL
            // url = "http://localhost:5173/game?token=TU_TOKEN_DE_PRUEBA"; 
            
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

    private IEnumerator FetchUserProfile(string token)
    {
        string baseUrl = ApiResultadoSender.BASE_URL; // Usa la URL condicional (Local vs Render)
        string url = $"{baseUrl}/me";

        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.SetRequestHeader("Authorization", $"Bearer {token}");
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
                        // Guardar el ID del usuario como alumno_id
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
                Debug.LogError($"[UnityNetworkManager] Error al cargar perfil (/me): {req.error} | {req.downloadHandler.text}");
            }
        }
    }
}
