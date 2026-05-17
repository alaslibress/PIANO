using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using System.Collections;
using System.Collections.Generic;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

public class InputManager : MonoBehaviour
{
    [Header("Configuración de Capas")]
    public LayerMask noteLayer;

    [Header("Referencias")]
    public Camera cam;

    // IDs de propiedad de shader cacheados. Evita lookups por string en cada flash.
    // URP usa _BaseColor; Built-in/Sprite usa _Color. Resolvemos al iniciar.
    private static readonly int _BaseColorID = Shader.PropertyToID("_BaseColor");
    private static readonly int _ColorID     = Shader.PropertyToID("_Color");

    // Bloque de propiedades reutilizable: evita clonar materiales (.material crea fugas en WebGL).
    private MaterialPropertyBlock _mpb;

    // Cache de los MeshRenderer de cada carril para no hacer GameObject.Find en caliente.
    private Dictionary<int, MeshRenderer> _laneCache;

    // Cache de RehabMetrics para no recorrer la escena en cada acierto.
    private RehabMetrics _metricsCache;

    // Cache del spawner: las notas se devuelven al pool al ser acertadas.
    private NoteSpawner _spawnerCache;

    void OnEnable()
    {
        // EnhancedTouch necesario para leer touches via InputSystem.EnhancedTouch.Touch
        EnhancedTouchSupport.Enable();
    }

    void OnDisable()
    {
        EnhancedTouchSupport.Disable();
    }

    void Start()
    {
        if (cam == null) cam = Camera.main;

        // Inicializa el property block (una sola asignacion, sin churn de materiales).
        _mpb = new MaterialPropertyBlock();

        // Pre-cachea las referencias a los carriles Lane_0..Lane_4.
        _laneCache = new Dictionary<int, MeshRenderer>(5);
        for (int i = 0; i < 5; i++)
        {
            GameObject lane = GameObject.Find("Lane_" + i);
            if (lane != null)
            {
                MeshRenderer mr = lane.GetComponent<MeshRenderer>();
                if (mr != null) _laneCache[i] = mr;
            }
        }

        // Cachea las referencias a singletons de escena que antes se buscaban en cada hit.
        _metricsCache = Object.FindFirstObjectByType<RehabMetrics>();
        _spawnerCache = Object.FindFirstObjectByType<NoteSpawner>();
    }

    void Update()
    {
        // Touch (mobile / WebGL touch). Prioridad sobre raton si hay dedos.
        if (Touch.activeTouches.Count > 0)
        {
            foreach (var t in Touch.activeTouches)
            {
                if (t.phase == UnityEngine.InputSystem.TouchPhase.Began)
                    CheckHit(t.screenPosition);
            }
            return;
        }

        // Raton (desktop). Mouse.current puede ser null en builds sin raton.
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            CheckHit(mouse.position.ReadValue());
        }
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    // En WebGL el canvas puede perder el foco si el usuario clica fuera.
    // Al recuperar el foco, re-obtener la camara principal por si cambio.
    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && cam == null)
            cam = Camera.main;
    }
#endif

    void CheckHit(Vector2 screenPosition)
    {
        Vector3 worldPoint = cam.ScreenToWorldPoint(screenPosition);
        worldPoint.z = 0;

        RaycastHit2D hit = Physics2D.Raycast(worldPoint, Vector2.zero, 100f, noteLayer);

        if (hit.collider != null)
        {
            NoteController note = hit.collider.GetComponent<NoteController>();

            if (note != null)
            {
                // 1. EFECTO VISUAL VIA MaterialPropertyBlock (sin clonar material).
                MeshRenderer laneMR;
                if (_laneCache != null && _laneCache.TryGetValue(note.fingerIndex, out laneMR) && laneMR != null)
                {
                    StartCoroutine(FlashLaneMesh(laneMR));
                }

                // 2. METRICAS via referencia cacheada.
                if (_metricsCache != null)
                {
                    _metricsCache.RegisterHit(note.fingerIndex, 0.1f);
                    Debug.Log("<color=green>¡Métrica registrada!</color> Dedo: " + note.fingerIndex);
                }

                // 3. DEVOLVER NOTA AL POOL en vez de destruirla.
                if (_spawnerCache != null)
                {
                    _spawnerCache.ReturnNote(hit.collider.gameObject);
                }
                else
                {
                    // Fallback defensivo: si no hay spawner, evitar dejar la nota viva.
                    Destroy(hit.collider.gameObject);
                }
                Debug.Log("<color=cyan>¡Nota reciclada!</color>");
            }
        }
    }

    // Corrutina que usa MaterialPropertyBlock: no instancia un material por carril.
    // Asi evitamos la fuga acumulativa de materiales en WebGL (causante de crash en Chromium).
    IEnumerator FlashLaneMesh(MeshRenderer laneMR)
    {
        // Detecta que propiedad de color usa el shader del material compartido.
        // sharedMaterial NO clona el material (mientras que .material si lo haria).
        Material shared = laneMR.sharedMaterial;
        int colorProp;
        if (shared != null && shared.HasProperty(_BaseColorID))      colorProp = _BaseColorID;
        else if (shared != null && shared.HasProperty(_ColorID))     colorProp = _ColorID;
        else                                                          colorProp = _BaseColorID;

        // Leemos el bloque actual (si existe), aplicamos color flash, lo escribimos.
        laneMR.GetPropertyBlock(_mpb);
        Color originalColor = shared != null ? shared.GetColor(colorProp) : Color.white;
        _mpb.SetColor(colorProp, new Color(0f, 1f, 1f, 1f));
        laneMR.SetPropertyBlock(_mpb);

        yield return new WaitForSeconds(0.15f);

        // Restauramos el color original sobre el mismo bloque reutilizable.
        if (laneMR != null)
        {
            laneMR.GetPropertyBlock(_mpb);
            _mpb.SetColor(colorProp, originalColor);
            laneMR.SetPropertyBlock(_mpb);
        }
    }
}
