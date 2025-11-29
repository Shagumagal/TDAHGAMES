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

    [System.Serializable]
    public class LoginPasswordBody
    {
        public string identifier;   // correo o username
        public string password;
    }

    // Botón en el menú contextual del componente
    [ContextMenu("Test /auth/login-password")]
    public void TestLogin()
    {
        StartCoroutine(LoginCoroutine());
    }

    private IEnumerator LoginCoroutine()
    {
        string url = apiBaseUrl.TrimEnd('/') + "/auth/login-password";

    var body = new LoginPasswordBody
{
    identifier = "alumno_123",     // o el username/correo del alumno
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
                Debug.LogError(
                    $"[TDAHApiTester] ERROR {request.responseCode} - {request.error}\n" +
                    request.downloadHandler.text
                );
            }
            else
            {
                Debug.Log($"[TDAHApiTester] OK {request.responseCode}: {request.downloadHandler.text}");
            }
        }
    }
}
