using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Text;
using System.Collections;
using TMPro;

[Serializable] public class UserInfo { public long id; public string role; public string nombre; }
[Serializable] public class ExchangeResp { public string game_jwt; public UserInfo user; }

[Serializable] public class JwtPayload {
    public string sub;
    public string role;
    public string scope;
    public string aud;
    public long   exp;
}

public class GameAuth : MonoBehaviour
{
    [Header("Config")]
    public string ApiBase = "http://localhost:4000";
    public bool debugLogs = false;

    public static string  GameJwt;
    public static UserInfo CurrentUser;

    // 🔔 Evento para avisar cuando ya tenemos al usuario
    public static event Action<UserInfo> OnUserReady;

    void Start() { StartCoroutine(ExchangeAndNotify()); }

    IEnumerator ExchangeAndNotify()
    {
        var code = GetQueryParam("code");
        if (string.IsNullOrEmpty(code)) { LogError("Falta ?code="); yield break; }

        using (var www = UnityWebRequest.Get($"{ApiBase}/game/exchange?code=" + WWW.EscapeURL(code)))
        {
            www.SetRequestHeader("Content-Type", "application/json");
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                LogError("exchange error: " + www.downloadHandler.text);
                yield break;
            }

            var resp = JsonUtility.FromJson<ExchangeResp>(www.downloadHandler.text);
            GameJwt     = resp.game_jwt;
            CurrentUser = resp.user;

            if (CurrentUser != null)
            {
                Log($"Usuario → id={CurrentUser.id}, rol={CurrentUser.role}, nombre={CurrentUser.nombre}");
                // 🔔 Notifica a quien esté escuchando (UserBadgeUI)
                OnUserReady?.Invoke(CurrentUser);
            }
        }
    }

    // === Helpers ===
    string GetQueryParam(string name)
    {
        var url = Application.absoluteURL;
        var q = url.IndexOf('?'); if (q < 0) return null;
        var parts = url.Substring(q + 1).Split('&');
        foreach (var kv in parts)
        {
            var p = kv.Split('='); if (p.Length == 2 && p[0] == name) return WWW.UnEscapeURL(p[1]);
        }
        return null;
    }

    void Log(string msg)      { if (debugLogs) Debug.Log($"[GameAuth] {msg}"); }
    void LogError(string msg) { Debug.LogError($"[GameAuth] {msg}"); }
}
