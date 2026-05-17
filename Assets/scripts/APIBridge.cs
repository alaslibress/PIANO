using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;

/// <summary>
/// Wrapper de bajo nivel para peticiones HTTP al API Core de RehabiAPP.
/// Para la telemetria de juego usar TelemetryUploader — incluye reintentos
/// y serializa el contrato polimorfrico completo.
/// Este componente queda para uso generico (endpoints no relacionados con telemetria).
/// </summary>
public class APIBridge : MonoBehaviour
{
    [Header("Configuracion")]
    // Dejar vacio — en WebGL se rellena desde URL param ?api= o desde TelemetryUploader.
    // En editor/escritorio poner http://localhost:8080 para pruebas locales.
    public string apiBaseUrl = "";

    void Awake()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // Si el Inspector no tiene URL, leer el param ?api= de la URL de la pagina
        if (string.IsNullOrEmpty(apiBaseUrl))
        {
            string absUrl  = Application.absoluteURL;
            string apiParam = ExtraerUrlParam(absUrl, "api");
            if (!string.IsNullOrEmpty(apiParam))
                apiBaseUrl = apiParam;
        }

        // Advertir mixed content antes de que las peticiones fallen en el navegador
        if (!string.IsNullOrEmpty(apiBaseUrl) &&
            Application.absoluteURL.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
            apiBaseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogError("[APIBridge] MIXED CONTENT: pagina HTTPS pero apiBaseUrl es HTTP. Las peticiones fallarán.");
        }
#endif
    }

    public void SendRequest(string endpoint, string jsonPayload)
    {
        StartCoroutine(PostRequest(apiBaseUrl + endpoint, jsonPayload));
    }

    IEnumerator PostRequest(string url, string json)
    {
        var request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        request.uploadHandler   = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("[APIBridge] Error en peticion a " + url + ": " + request.error
                + " (HTTP " + request.responseCode + ")");
        }
        else
        {
            Debug.Log("[APIBridge] Respuesta de " + url + ": " + request.downloadHandler.text);
        }
    }

    // Extrae el valor de un parametro de la query string de la URL.
    private string ExtraerUrlParam(string url, string param)
    {
        int queryStart = url.IndexOf('?');
        if (queryStart < 0) return null;
        string query = url.Substring(queryStart + 1);

        foreach (string pair in query.Split('&'))
        {
            int eq = pair.IndexOf('=');
            if (eq < 0) continue;
            if (pair.Substring(0, eq) == param)
                return Uri.UnescapeDataString(pair.Substring(eq + 1));
        }
        return null;
    }
}
