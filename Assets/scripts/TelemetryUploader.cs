using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.IO;
using System.Text;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

public class TelemetryUploader : MonoBehaviour
{
    [Header("Configuracion de API")]
    // Dejar vacio — en WebGL se rellena desde URL params (?api=) o mismo origen.
    // En editor/escritorio poner http://localhost:8080 para pruebas locales.
    public string apiBaseUrl = "";
    public string jwtToken = "";

    [Header("Autenticacion automatica")]
    [Tooltip("DNI del paciente. En WebGL se lee de URL param ?dni=.")]
    public string patientDni = "";
    [Tooltip("Contrasena del paciente. NO rellenar en builds WebGL — usar ?token= en URL.")]
    public string patientPassword = "";

    // Overrides de contexto leidos de la URL en WebGL (?cod_tratamiento=, ?disability_id=, ?parte_cuerpo=).
    // GameManager los lee al construir el payload. Vacios = se usa el valor del Inspector.
    [System.NonSerialized] public string codTratamientoOverride = "";
    [System.NonSerialized] public string disabilityIdOverride   = "";
    [System.NonSerialized] public string parteCuerpoOverride    = "";

    [Header("Modo Prueba Local")]
    [Tooltip("En escritorio guarda JSON en disco. En WebGL imprime en consola del navegador.")]
    public bool localTestMode = false;
    [Tooltip("Ruta de exportacion local. Solo aplica en escritorio/editor.")]
    public string localExportPath = "";

    [Header("Reintentos")]
    public int maxRetries = 3;
    private static readonly float[] BackoffSeconds = { 2f, 8f, 32f };
    private const string ENDPOINT = "/api/telemetria/sesion-juego";
    private const string LOGIN_ENDPOINT = "/api/auth/login-paciente";

#if UNITY_WEBGL && !UNITY_EDITOR
    // Via 2 (postMessage/jslib): el host asigna window._rehabToken/Dni/ApiUrl antes de cargar Unity.
    [DllImport("__Internal")] private static extern string RehabGetToken();
    [DllImport("__Internal")] private static extern string RehabGetDni();
    [DllImport("__Internal")] private static extern string RehabGetApiUrl();
    // Notifica al frame padre (WebView/browser) que la sesion fue enviada correctamente.
    [DllImport("__Internal")] private static extern void NotificarSesionEnviada();
#endif

    void Awake()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // Via 1 (jslib): lee URL params via window.location.search en JS (fiable en todos los templates).
        // El bridge tambien soporta window._rehabToken como fallback para hosts que inyectan via JS.
        try
        {
            string t = RehabGetToken();
            string d = RehabGetDni();
            string a = RehabGetApiUrl();
            if (!string.IsNullOrEmpty(t)) jwtToken   = t;
            if (!string.IsNullOrEmpty(d)) patientDni = d;
            if (!string.IsNullOrEmpty(a)) apiBaseUrl = a;
        }
        catch { /* jslib no disponible — caer a Via 2 */ }

        string absUrl = Application.absoluteURL;

        // Via 2 (fallback C#): Application.absoluteURL puede no incluir query en algunos templates,
        // pero si lo trae permite override sin jslib.
        if (string.IsNullOrEmpty(jwtToken))
        {
            string tokenParam = ExtraerUrlParam(absUrl, "token");
            if (!string.IsNullOrEmpty(tokenParam)) jwtToken = tokenParam;
        }
        if (string.IsNullOrEmpty(patientDni))
        {
            string dniParam = ExtraerUrlParam(absUrl, "dni");
            if (!string.IsNullOrEmpty(dniParam)) patientDni = dniParam;
        }
        if (string.IsNullOrEmpty(apiBaseUrl) || apiBaseUrl.Contains("localhost"))
        {
            string apiParam = ExtraerUrlParam(absUrl, "api");
            if (!string.IsNullOrEmpty(apiParam)) apiBaseUrl = apiParam;
        }

        // Contexto opcional de tratamiento/discapacidad/parte del cuerpo
        codTratamientoOverride = ExtraerUrlParam(absUrl, "cod_tratamiento") ?? "";
        disabilityIdOverride   = ExtraerUrlParam(absUrl, "disability_id")   ?? "";
        parteCuerpoOverride    = ExtraerUrlParam(absUrl, "parte_cuerpo")    ?? "";

        // Si apiBaseUrl sigue vacio, derivar mismo origen que la pagina
        if (string.IsNullOrEmpty(apiBaseUrl))
        {
            Uri uri = new Uri(absUrl);
            apiBaseUrl = uri.Scheme + "://" + uri.Host +
                         (uri.IsDefaultPort ? "" : ":" + uri.Port);
            Debug.LogWarning("[TelemetryUploader] ?api= no recibido, usando mismo origen: " + apiBaseUrl);
        }

        // Mixed content: pagina HTTPS con API HTTP = el navegador bloquea la peticion
        if (absUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
            apiBaseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogError("[TelemetryUploader] MIXED CONTENT: la pagina es HTTPS pero apiBaseUrl es HTTP. " +
                           "Los uploads fallarán. Configura ?api= con una URL HTTPS.");
        }
#endif
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

    public void Submit(TelemetryPayload payload)
    {
        string json = SerializarPayload(payload);

        if (localTestMode)
        {
            ExportarEnLocal(json, payload);
            return;
        }

        StartCoroutine(SubmitConAuth(json, payload));
    }

    // Si no hay token intenta hacer login primero, luego envia la telemetria.
    IEnumerator SubmitConAuth(string json, TelemetryPayload payload)
    {
        if (string.IsNullOrEmpty(jwtToken))
        {
            if (string.IsNullOrEmpty(patientPassword))
            {
                Debug.LogError("[TelemetryUploader] jwtToken vacio y patientPassword no configurado. " +
                               "En WebGL: usa ?token= en la URL. En editor: rellena patientPassword en Inspector.");
                yield break;
            }
            yield return StartCoroutine(Login(payload));
        }

        if (!string.IsNullOrEmpty(jwtToken))
            yield return StartCoroutine(PostConReintentos(json, 0));
    }

    IEnumerator Login(TelemetryPayload payload)
    {
        string loginDni  = !string.IsNullOrEmpty(patientDni) ? patientDni : payload.dniPaciente;
        string loginJson = "{\"identifier\":\"" + loginDni + "\",\"contrasena\":\"" + patientPassword + "\"}";
        byte[] body      = Encoding.UTF8.GetBytes(loginJson);

        using var req = new UnityWebRequest(apiBaseUrl + LOGIN_ENDPOINT, "POST");
        req.uploadHandler   = new UploadHandlerRaw(body);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            // Extrae el token del JSON de respuesta sin depender de JsonUtility
            string respuesta = req.downloadHandler.text;
            jwtToken = ExtraerCampoJson(respuesta, "token");
            if (string.IsNullOrEmpty(jwtToken))
                jwtToken = ExtraerCampoJson(respuesta, "accessToken");

            if (!string.IsNullOrEmpty(jwtToken))
                Debug.Log("[TelemetryUploader] Login correcto. Token obtenido.");
            else
                Debug.LogError("[TelemetryUploader] Login OK pero token no encontrado en: " + respuesta);
        }
        else
        {
            Debug.LogError("[TelemetryUploader] Login fallido (" + req.responseCode + "): " + req.downloadHandler.text);
        }
    }

    // Extrae el valor de un campo string de un JSON plano sin parser externo.
    private string ExtraerCampoJson(string json, string campo)
    {
        string buscar = "\"" + campo + "\":\"";
        int inicio = json.IndexOf(buscar, StringComparison.Ordinal);
        if (inicio < 0) return null;
        inicio += buscar.Length;
        int fin = json.IndexOf('"', inicio);
        return fin < 0 ? null : json.Substring(inicio, fin - inicio);
    }

    // En escritorio/editor guarda el JSON en disco.
    // En WebGL imprime en consola del navegador (sin acceso a disco).
    private void ExportarEnLocal(string json, TelemetryPayload payload)
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        try
        {
            string basePath = string.IsNullOrEmpty(localExportPath)
                ? Application.persistentDataPath
                : localExportPath;

            string timestamp  = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string nombre     = $"telemetria_{payload.dniPaciente}_{timestamp}.json";
            string rutaCompleta = Path.Combine(basePath, nombre);

            File.WriteAllText(rutaCompleta, json, Encoding.UTF8);

            Debug.Log("[TelemetryUploader] MODO LOCAL — JSON guardado en: " + rutaCompleta);
            Debug.Log("[TelemetryUploader] Contenido:\n" + json);
        }
        catch (Exception e)
        {
            Debug.LogError("[TelemetryUploader] Error al guardar JSON local: " + e.Message);
        }
#else
        Debug.Log("[TelemetryUploader] MODO LOCAL (WebGL) — Payload que se enviaria:\n" + json);
#endif
    }

    IEnumerator PostConReintentos(string json, int intento)
    {
        string url  = apiBaseUrl + ENDPOINT;
        byte[] body = Encoding.UTF8.GetBytes(json);

        using var request = new UnityWebRequest(url, "POST");
        request.uploadHandler   = new UploadHandlerRaw(body);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        if (!string.IsNullOrEmpty(jwtToken))
            request.SetRequestHeader("Authorization", "Bearer " + jwtToken);

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("[TelemetryUploader] Sesion enviada correctamente. Respuesta: " + request.downloadHandler.text);
#if UNITY_WEBGL && !UNITY_EDITOR
            // Notificar al frame padre (WebView mobile) que la sesion termino
            try { NotificarSesionEnviada(); } catch { }
#endif
            yield break;
        }

        if (request.responseCode >= 400 && request.responseCode < 500)
        {
            Debug.LogError("[TelemetryUploader] Error de contrato (" + request.responseCode + "): " + request.downloadHandler.text);
            yield break;
        }

        if (intento < maxRetries)
        {
            float espera = intento < BackoffSeconds.Length ? BackoffSeconds[intento] : BackoffSeconds[^1];
            Debug.LogWarning("[TelemetryUploader] Reintentando en " + espera + "s...");
            yield return new WaitForSeconds(espera);
            yield return PostConReintentos(json, intento + 1);
        }
        else
        {
            Debug.LogError("[TelemetryUploader] Fallaron " + maxRetries + " intentos. Payload: " + json);
        }
    }

    private string SerializarPayload(TelemetryPayload p)
    {
        var sb = new StringBuilder();
        sb.Append("{");
        sb.Append("\"dniPaciente\":\"").Append(p.dniPaciente).Append("\",");
        sb.Append("\"videojuegoCodigo\":\"").Append(p.videojuegoCodigo).Append("\",");

        // Campos opcionales — solo se incluyen si tienen valor
        if (!string.IsNullOrEmpty(p.disabilityId))
            sb.Append("\"disabilityId\":\"").Append(p.disabilityId).Append("\",");
        if (!string.IsNullOrEmpty(p.codTratamiento))
            sb.Append("\"codTratamiento\":\"").Append(p.codTratamiento).Append("\",");
        if (!string.IsNullOrEmpty(p.parteCuerpo))
            sb.Append("\"parteCuerpo\":\"").Append(p.parteCuerpo).Append("\",");

        sb.Append("\"inicio\":\"").Append(p.inicio).Append("\",");
        sb.Append("\"fin\":\"").Append(p.fin).Append("\",");
        sb.Append("\"duracionMs\":").Append(p.duracionMs).Append(",");
        sb.Append("\"puntuacion\":").Append(p.puntuacion).Append(",");
        sb.Append("\"metricas\":{");
        if (p.metricas != null)
        {
            bool primero = true;
            foreach (var par in p.metricas)
            {
                if (!primero) sb.Append(",");
                primero = false;
                sb.Append("\"").Append(par.Key).Append("\":").Append(SerializarValor(par.Value));
            }
        }
        sb.Append("}}");
        return sb.ToString();
    }

    private string SerializarValor(object v) => v switch {
        int i    => i.ToString(),
        float f  => f.ToString("F4", System.Globalization.CultureInfo.InvariantCulture),
        double d => d.ToString("F4", System.Globalization.CultureInfo.InvariantCulture),
        bool b   => b ? "true" : "false",
        _        => "\"" + v + "\""
    };
}
