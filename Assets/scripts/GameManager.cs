using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;

// Fuerza que estos componentes existan en el mismo GameObject que GameManager.
// Unity los añade automaticamente al arrastrar GameManager a cualquier objeto.
[RequireComponent(typeof(TelemetryUploader))]
[RequireComponent(typeof(RehabMetrics))]
[RequireComponent(typeof(AudioSource))]
public class GameManager : MonoBehaviour
{
    [Header("Interfaz de Usuario")]
    public GameObject menuPanel;
    public Text countdownText;
    public Button btnFacil;
    public Button btnMedio;
    public Button btnDificil;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip[] musicTracks = new AudioClip[3];

    [Header("Conexiones y Ajustes")]
    public NoteSpawner noteSpawner;
    public RehabMetrics metrics;
    public TelemetryUploader telemetryUploader;
    public float levelDuration = 60f;

    [Header("Datos de Sesion (Contrato RehabiAPP)")]
    public string patientDni = "12345678X"; // Rellenar desde la app móvil
    public string gameCode = "PIANO-001";
    public string disabilityId = "";        // Opcional: ID de la discapacidad
    public string codTratamiento = "";      // Opcional: ID del tratamiento
    public string parteCuerpo = "Mano Derecha";

    private int currentDifficulty = 1;
    private DateTime sessionStart;

    void Awake()
    {
        // RequireComponent garantiza que estos componentes existen en el mismo GO.
        // GetComponent nunca devuelve null aqui.
        if (metrics == null)           metrics = GetComponent<RehabMetrics>();
        if (telemetryUploader == null) telemetryUploader = GetComponent<TelemetryUploader>();
        if (audioSource == null)       audioSource = GetComponent<AudioSource>();
    }

    void Start()
    {
        menuPanel.SetActive(true);
        countdownText.gameObject.SetActive(false);
        audioSource.Stop();

        btnFacil.onClick.AddListener(() => SelectDifficultyAndStart(1));
        btnMedio.onClick.AddListener(() => SelectDifficultyAndStart(2));
        btnDificil.onClick.AddListener(() => SelectDifficultyAndStart(3));
    }

    public void SelectDifficultyAndStart(int difficulty)
    {
        currentDifficulty = difficulty;
        menuPanel.SetActive(false);
        StartCoroutine(StartCountdown());
    }

    IEnumerator StartCountdown()
    {
        countdownText.gameObject.SetActive(true);
        countdownText.fontSize = 200;

        countdownText.text = "3"; yield return new WaitForSeconds(1f);
        countdownText.text = "2"; yield return new WaitForSeconds(1f);
        countdownText.text = "1"; yield return new WaitForSeconds(1f);
        countdownText.text = "A JUGAR"; yield return new WaitForSeconds(0.5f);

        countdownText.gameObject.SetActive(false);
        StartGame();
    }

    void StartGame()
    {
        sessionStart = DateTime.UtcNow;

        float noteSpeed = 4f;
        float spawnInterval = 1.5f;

        if (currentDifficulty == 1) { noteSpeed = 4f; spawnInterval = 1.5f; audioSource.clip = musicTracks[0]; }
        else if (currentDifficulty == 2) { noteSpeed = 7f; spawnInterval = 1.0f; audioSource.clip = musicTracks[1]; }
        else if (currentDifficulty == 3) { noteSpeed = 10f; spawnInterval = 0.6f; audioSource.clip = musicTracks[2]; }

        if (audioSource.clip != null) audioSource.Play();

        noteSpawner.StartSpawning(noteSpeed, spawnInterval);
        StartCoroutine(LevelTimer());
    }

    IEnumerator LevelTimer()
    {
        yield return new WaitForSeconds(levelDuration);
        EndGame();
    }

    // Método para forzar el fin del juego (Útil para testear sin esperar 60s)
    [ContextMenu("Forzar Fin de Juego")]
    public void EndGame()
    {
        StopAllCoroutines(); // Detiene el temporizador si se fuerza el fin
        noteSpawner.StopSpawning();
        audioSource.Stop();

        DateTime sessionEnd = DateTime.UtcNow;
        int finalScore = 0;

        if (metrics != null)
        {
            // Calculamos el índice de rendimiento
            finalScore = Mathf.RoundToInt(metrics.CalculateIRS(currentDifficulty));
            EnviarTelemetria(sessionEnd, finalScore);
        }

        countdownText.gameObject.SetActive(true);
        countdownText.fontSize = 80;
        countdownText.text = "SESION COMPLETADA\nIRS: " + finalScore;
    }

    private void EnviarTelemetria(DateTime sessionEnd, int score)
    {
        if (telemetryUploader == null)
        {
            Debug.LogWarning("[GameManager] TelemetryUploader no asignado.");
            return;
        }

        long duracionMs = (long)(sessionEnd - sessionStart).TotalMilliseconds;

        // Resolvemos el paciente y los IDs efectivos. En WebGL, TelemetryUploader
        // ya leyo ?dni=, ?cod_tratamiento=, ?disability_id= y ?parte_cuerpo= de la
        // URL en Awake. Si esos valores existen tienen prioridad sobre los del
        // Inspector — asi varios pacientes pueden jugar el mismo build y cada
        // sesion va al usuario correcto.
        string dniEfectivo           = !string.IsNullOrEmpty(telemetryUploader.patientDni)
                                        ? telemetryUploader.patientDni : patientDni;
        string disabilityEfectiva    = !string.IsNullOrEmpty(telemetryUploader.disabilityIdOverride)
                                        ? telemetryUploader.disabilityIdOverride : disabilityId;
        string tratamientoEfectivo   = !string.IsNullOrEmpty(telemetryUploader.codTratamientoOverride)
                                        ? telemetryUploader.codTratamientoOverride : codTratamiento;
        string parteCuerpoEfectiva   = !string.IsNullOrEmpty(telemetryUploader.parteCuerpoOverride)
                                        ? telemetryUploader.parteCuerpoOverride : parteCuerpo;

        if (string.IsNullOrEmpty(dniEfectivo) || dniEfectivo == "12345678X")
            Debug.LogWarning("[GameManager] DNI efectivo es vacio o placeholder. " +
                             "Asegurate que la URL incluye ?dni=DNI_REAL.");

        // Construimos el payload siguiendo el contrato de Claude
        var payload = new TelemetryPayload
        {
            dniPaciente       = dniEfectivo,
            videojuegoCodigo  = gameCode,
            disabilityId      = disabilityEfectiva,
            codTratamiento    = tratamientoEfectivo,
            parteCuerpo       = parteCuerpoEfectiva,
            inicio            = sessionStart.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            fin               = sessionEnd.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            duracionMs        = duracionMs,
            puntuacion        = score,
            // Aquí pedimos a RehabMetrics que genere el Diccionario dinámico
            metricas          = metrics.GetDynamicMetrics(currentDifficulty, duracionMs / 1000f)
        };

        telemetryUploader.Submit(payload);
    }
}