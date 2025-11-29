// GameResultsSender.cs
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;

public class GameResultsSender : MonoBehaviour {
    public IEnumerator EnviarResultados(object resumen) {
        var json = JsonUtility.ToJson(resumen); // o tu propio JSON
        var bytes = Encoding.UTF8.GetBytes(json);
        var req = new UnityWebRequest($"{WebGLGameAuth.ApiUrl}/game/submit", "POST");
        req.uploadHandler = new UploadHandlerRaw(bytes);
        req.downloadHandler = new DownloadHandlerBuffer();
        WebGLGameAuth.WithAuth(req);
        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success)
            Debug.LogError("submit error: " + req.error + " body: " + req.downloadHandler.text);
        else
            Debug.Log("submit OK");
    }
}
