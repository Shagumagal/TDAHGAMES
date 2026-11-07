// WebGLGameAuth.cs
// NOTA: Toda la lógica de autenticación se movió a UnityNetworkManager.cs
// Este archivo se mantiene solo para compatibilidad (WithAuth helper + campos estáticos)
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Text;
using System.Linq;

public class WebGLGameAuth : MonoBehaviour {
#if UNITY_EDITOR
    public static string ApiUrl = "http://localhost:4000";
#else
    public static string ApiUrl = "https://proyectotdah.onrender.com";
#endif
    public static string GameJwt;
    public static string UserId;
    public static string UserRole;
    public static string UserNombre;

    // NO llamar a AuthFlow aquí - UnityNetworkManager se encarga de todo
    // void Start() { }

    public static UnityWebRequest WithAuth(UnityWebRequest req) {
        // Usar el token de UnityNetworkManager si existe
        string token = UnityNetworkManager.GameJwt ?? GameJwt;
        if (!string.IsNullOrEmpty(token))
            req.SetRequestHeader("Authorization", "Bearer " + token);
        req.SetRequestHeader("Content-Type", "application/json");
        return req;
    }
}
