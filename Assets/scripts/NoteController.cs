using UnityEngine;

public class NoteController : MonoBehaviour
{
    public float speed;
    public int fingerIndex; // 0=Pulgar, 4=Meñique

    // Referencias cacheadas al spawner y a las metricas. Evitan FindObjectOfType
    // en cada Update (potencialmente cientos de notas por sesion en WebGL).
    private NoteSpawner _spawner;
    private RehabMetrics _metrics;

    void Start()
    {
        // Cacheamos una vez. Si la nota se reutiliza desde el pool, las referencias
        // siguen siendo validas porque viven mientras dure la escena.
        if (_spawner == null) _spawner = Object.FindFirstObjectByType<NoteSpawner>();
        if (_metrics == null) _metrics = Object.FindFirstObjectByType<RehabMetrics>();
    }

    void Update()
    {
        // Movimiento hacia abajo
        transform.Translate(Vector3.down * speed * Time.deltaTime);

        // Si la nota sale de la pantalla por abajo, es un fallo
        if (transform.position.y < -6f)
        {
            if (_metrics != null) _metrics.RegisterMiss(fingerIndex);

            // Devolvemos la nota al pool en lugar de destruirla.
            if (_spawner != null) _spawner.ReturnNote(gameObject);
            else                  Destroy(gameObject); // Fallback defensivo
        }
    }
}
