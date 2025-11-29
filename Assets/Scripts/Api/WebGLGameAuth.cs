// WebGLGameAuth.cs
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Text;
using System.Linq;

public class WebGLGameAuth : MonoBehaviour {
    public static string ApiUrl = "http://localhost:4000";  // setéalo por Inspector si quieres
    public static string GameJwt;  // token scope=game
    public static string UserId;
    public static string UserRole;
    public static string UserNombre;

    void Start() { StartCoroutine(ExchangeCodeCoroutine()); }

    static string GetQueryParam(string name) {
        var url = Application.absoluteURL; // WebGL only
        var qIdx = url.IndexOf('?');
        if (qIdx < 0) return null;
        var query = url.Substring(qIdx + 1);
        foreach (var kv in query.Split('&')) {
            var parts = kv.Split('=');
            if (parts.Length == 2 && parts[0] == name)
                return Uri.UnescapeDataString(parts[1]);
        }
        return null;
    }

    IEnumerator ExchangeCodeCoroutine() {
        var code = GetQueryParam("code");
        if (string.IsNullOrEmpty(code)) { Debug.LogError("No code in URL"); yield break; }

        var req = UnityWebRequest.Get($"{ApiUrl}/game/exchange?code={UnityWebRequest.EscapeURL(code)}");
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success) {
            Debug.LogError("exchange error: " + req.error + " body: " + req.downloadHandler.text);
            yield break;
        }
        var json = req.downloadHandler.text;
        // parse minimísima (o usa Newtonsoft si lo tienes)
        var tokKey = "\"game_jwt\":\"";
        var tIdx = json.IndexOf(tokKey);
        if (tIdx < 0) { Debug.LogError("No game_jwt"); yield break; }
        var start = tIdx + tokKey.Length;
        var end = json.IndexOf('"', start);
        GameJwt = json.Substring(start, end - start);

        // (Opcional) parse user.id / role / nombre
        Debug.Log("Game auth OK");
    }

    public static UnityWebRequest WithAuth(UnityWebRequest req) {
        if (!string.IsNullOrEmpty(GameJwt))
            req.SetRequestHeader("Authorization", "Bearer " + GameJwt);
        req.SetRequestHeader("Content-Type", "application/json");
        return req;
    }
}
