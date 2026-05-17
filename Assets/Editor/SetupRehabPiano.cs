using UnityEngine;
using UnityEditor;

public class SetupRehabPiano : EditorWindow
{
    [MenuItem("RehabiAPP/Setup Piano Scene")]
    public static void CreatePianoScene()
    {
        // 1. Crear el Manager de Lógica
        GameObject logicManager = new GameObject("LogicManager");
        logicManager.AddComponent<RehabMetrics>();
        logicManager.AddComponent<APIBridge>();
        
        // 2. Crear la Cámara principal configurada
        Camera cam = Camera.main;
        if (cam == null) {
            GameObject camObj = new GameObject("Main Camera");
            cam = camObj.AddComponent<Camera>();
        }
        cam.orthographic = true;
        cam.orthographicSize = 5;
        cam.backgroundColor = new Color(0.1f, 0.1f, 0.1f);

        // 3. Crear el Escenario (Carriles)
        GameObject environment = new GameObject("Environment");
        float startX = -4f;
        float spacing = 2f;
        Transform[] spawnPoints = new Transform[5];

        for (int i = 0; i < 5; i++)
        {
            GameObject lane = GameObject.CreatePrimitive(PrimitiveType.Quad);
            lane.name = "Lane_" + i;
            lane.transform.SetParent(environment.transform);
            lane.transform.position = new Vector3(startX + (i * spacing), 0, 0);
            lane.transform.localScale = new Vector3(1.8f, 10f, 1f);
            
            // Color semitransparente para el carril
            Renderer r = lane.GetComponent<Renderer>();
            r.material = new Material(Shader.Find("Sprites/Default"));
            r.material.color = new Color(1, 1, 1, 0.05f);
            DestroyImmediate(lane.GetComponent<MeshCollider>());

            // Crear Punto de Spawn (arriba)
            GameObject spawnPoint = new GameObject("SpawnPoint_" + i);
            spawnPoint.transform.SetParent(lane.transform);
            spawnPoint.transform.position = new Vector3(lane.transform.position.x, 6f, 0);
            spawnPoints[i] = spawnPoint.transform;
        }

        // 4. Crear la Hit Zone (Zona de impacto abajo)
        GameObject hitZone = new GameObject("HitZone");
        hitZone.transform.position = new Vector3(0, -4f, 0);
        hitZone.AddComponent<InputManager>();
        // Importante: Asignar Layer para el Raycast (Debes crear la Layer "NoteLayer" en Unity)
        hitZone.layer = LayerMask.NameToLayer("Default"); 

        for (int i = 0; i < 5; i++)
        {
            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            target.name = "Target_" + i;
            target.transform.SetParent(hitZone.transform);
            target.transform.position = new Vector3(startX + (i * spacing), -4f, 0);
            target.transform.localScale = new Vector3(1.2f, 1.2f, 1f);
            target.GetComponent<Renderer>().material.color = Color.cyan;
            DestroyImmediate(target.GetComponent<SphereCollider>());
        }

        // 5. Crear el Spawner
        GameObject spawnerObj = new GameObject("NoteSpawner");
        NoteSpawner spawner = spawnerObj.AddComponent<NoteSpawner>();
        spawner.spawnPoints = spawnPoints;

        // 6. Crear un Prefab de Nota básico (Temporal)
        GameObject noteTemplate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        noteTemplate.name = "NotePrefab_Template";
        noteTemplate.AddComponent<NoteController>();
        noteTemplate.AddComponent<Rigidbody2D>().isKinematic = true;
        noteTemplate.AddComponent<BoxCollider2D>().isTrigger = true;
        noteTemplate.tag = "Finish"; // Usa un tag existente o crea uno nuevo
        
        Debug.Log("Estructura de RehabiAPP Piano creada. Recuerda asignar el Prefab de la nota al NoteSpawner.");
    }
}