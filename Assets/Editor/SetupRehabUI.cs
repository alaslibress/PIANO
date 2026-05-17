using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class SetupRehabUI : EditorWindow
{
    [MenuItem("RehabiAPP/Setup Game UI")]
    public static void CreateUI()
    {
        // 1. Crear EventSystem (Vital para que los botones detecten clics)
        if (FindObjectOfType<EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        // 2. Crear Canvas adaptable a móviles/tablets
        GameObject canvasObj = new GameObject("Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObj.AddComponent<GraphicRaycaster>();

        // 3. Crear el Panel del Menú (Fondo semi-transparente)
        GameObject panelObj = new GameObject("MenuPanel");
        panelObj.transform.SetParent(canvasObj.transform, false);
        RectTransform panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero; panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero; panelRect.offsetMax = Vector2.zero;
        Image panelImg = panelObj.AddComponent<Image>();
        panelImg.color = new Color(0, 0, 0, 0.8f);

        // 4. Crear los 3 Botones
        Button btnFacil = CreateButton("Btn_Facil", "Fácil", panelObj.transform, new Vector2(0, 150));
        Button btnMedio = CreateButton("Btn_Medio", "Medio", panelObj.transform, new Vector2(0, 0));
        Button btnDificil = CreateButton("Btn_Dificil", "Difícil", panelObj.transform, new Vector2(0, -150));

        // 5. Crear Texto de Cuenta Atrás
        GameObject textObj = new GameObject("CountdownText");
        textObj.transform.SetParent(canvasObj.transform, false);
        Text countdownText = textObj.AddComponent<Text>();
        countdownText.text = "3";
        countdownText.fontSize = 200;
        countdownText.alignment = TextAnchor.MiddleCenter;
        countdownText.color = Color.white;
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(1000, 400);

        // 6. Conectar todo al LogicManager
        GameObject logicManager = GameObject.Find("LogicManager");
        if (logicManager == null)
        {
            Debug.LogError("No se encontró el LogicManager. Asegúrate de tenerlo en la escena.");
            return;
        }

        AudioSource audioSource = logicManager.GetComponent<AudioSource>();
        if (audioSource == null) audioSource = logicManager.AddComponent<AudioSource>();
        audioSource.loop = true;
        audioSource.playOnAwake = false;

        RehabMetrics metrics = logicManager.GetComponent<RehabMetrics>();
        if (metrics == null) metrics = logicManager.AddComponent<RehabMetrics>();

        TelemetryUploader uploader = logicManager.GetComponent<TelemetryUploader>();
        if (uploader == null) uploader = logicManager.AddComponent<TelemetryUploader>();

        GameManager gm = logicManager.GetComponent<GameManager>();
        if (gm == null) gm = logicManager.AddComponent<GameManager>();

        gm.menuPanel = panelObj;
        gm.countdownText = countdownText;
        gm.btnFacil = btnFacil;
        gm.btnMedio = btnMedio;
        gm.btnDificil = btnDificil;
        gm.audioSource = audioSource;
        gm.metrics = metrics;
        gm.telemetryUploader = uploader;

        NoteSpawner spawner = FindObjectOfType<NoteSpawner>();
        if (spawner != null) gm.noteSpawner = spawner;

        Debug.Log("¡Interfaz de RehabiAPP generada y conectada! TelemetryUploader y RehabMetrics añadidos al LogicManager.");
    }

    // Función auxiliar para generar botones rápidamente
    private static Button CreateButton(string name, string textStr, Transform parent, Vector2 position)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);
        RectTransform rect = btnObj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(400, 100);
        rect.anchoredPosition = position;
        
        btnObj.AddComponent<Image>(); // Fondo blanco por defecto
        Button btn = btnObj.AddComponent<Button>();

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);
        Text txt = textObj.AddComponent<Text>();
        txt.text = textStr;
        txt.fontSize = 50;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.black;
        
        RectTransform txtRect = textObj.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero; txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero; txtRect.offsetMax = Vector2.zero;

        return btn;
    }
}