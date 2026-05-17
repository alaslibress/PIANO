using UnityEngine;
using System.Collections.Generic;

public class NoteSpawner : MonoBehaviour
{
    public GameObject notePrefab;
    public Transform[] spawnPoints;

    private float currentSpeed = 5f;
    private bool isPlaying = false;

    // Pool de notas reutilizables. Evita Instantiate/Destroy en cada nota,
    // que en WebGL provoca churn de GC y acumulacion de objetos en JS heap.
    private readonly Queue<GameObject> _pool = new Queue<GameObject>();

    // Esta función ahora la llama el GameManager cuando termina la cuenta atrás
    public void StartSpawning(float noteSpeed, float spawnInterval)
    {
        currentSpeed = noteSpeed;
        isPlaying = true;

        // Empieza a crear notas repetidamente (Espera 0 segundos para la primera, luego cada 'spawnInterval')
        InvokeRepeating("SpawnNote", 0f, spawnInterval);
    }

    public void StopSpawning()
    {
        isPlaying = false;
        CancelInvoke("SpawnNote"); // Detiene la creación de notas
    }

    // Obtiene una nota del pool o la instancia si el pool esta vacio.
    public GameObject GetNote()
    {
        if (_pool.Count > 0)
        {
            GameObject n = _pool.Dequeue();
            n.SetActive(true);
            return n;
        }
        return Instantiate(notePrefab);
    }

    // Devuelve una nota al pool desactivandola en lugar de destruirla.
    public void ReturnNote(GameObject n)
    {
        if (n == null) return;
        n.SetActive(false);
        _pool.Enqueue(n);
    }

    void SpawnNote()
    {
        if (!isPlaying) return;

        int randomIndex = Random.Range(0, spawnPoints.Length);

        // Reutiliza una nota del pool en vez de instanciarla cada vez.
        GameObject note = GetNote();
        note.transform.position = spawnPoints[randomIndex].position;
        note.transform.rotation = Quaternion.identity;

        NoteController controller = note.GetComponent<NoteController>();
        controller.speed = currentSpeed; // Le pasamos la velocidad según la dificultad
        controller.fingerIndex = randomIndex;
    }
}
