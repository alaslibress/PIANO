using UnityEngine;
using System.Collections.Generic;
using System.Linq; // Importante para usar .Sum()

public class RehabMetrics : MonoBehaviour
{
    [Header("Resultados por Dedo")]
    public int[] hitsPerFinger = new int[5];
    public int[] missesPerFinger = new int[5];

    [Header("Estadisticas de Sesion")]
    public int currentStreak = 0;
    public int maxStreak = 0;
    public int totalNotes = 0;
    private int totalHits = 0;
    private List<float> reactionTimes = new List<float>();

    public void RegisterHit(int finger, float timeInSeconds)
    {
        if (finger < 0 || finger >= 5) return;
        hitsPerFinger[finger]++;
        totalHits++;
        reactionTimes.Add(timeInSeconds);
        currentStreak++;
        if (currentStreak > maxStreak) maxStreak = currentStreak;
        totalNotes++;
    }

    public void RegisterMiss(int finger)
    {
        if (finger < 0 || finger >= 5) return;
        missesPerFinger[finger]++;
        currentStreak = 0;
        totalNotes++;
    }

    public float CalculateIRS(int difficulty)
    {
        if (totalNotes == 0) return 0;
        float P = ((float)totalHits / totalNotes) * 100f;
        float avgTR = GetAverageReactionTimeMS();
        float Tref = 1500f;
        float Vnorm = Mathf.Max(0, (Tref - avgTR) / Tref) * 100f;
        float Cnorm = ((float)maxStreak / totalNotes) * 100f;
        float D = difficulty == 1 ? 1.0f : difficulty == 2 ? 1.5f : 2.0f;
        return D * (0.60f * P + 0.30f * Vnorm + 0.10f * Cnorm);
    }

    public Dictionary<string, object> GetDynamicMetrics(int difficulty = 1, float durationSeconds = 60f)
    {
        return BuildRawMetrics(difficulty, durationSeconds);
    }

    public Dictionary<string, object> BuildRawMetrics(int difficulty, float durationSeconds = 60f)
    {
        int totalMisses = missesPerFinger.Sum();
        float accuracyPct = totalNotes > 0 ? ((float)totalHits / totalNotes) * 100f : 0f;
        // Requerido por PIANO-001 schema: velocidad media de notas acertadas por segundo
        float fingerSpeed = durationSeconds > 0 ? totalHits / durationSeconds : 0f;

        var dict = new Dictionary<string, object>
        {
            { "fingerSpeed",   System.Math.Round(fingerSpeed, 4) },
            { "notesHit",      totalHits },
            { "notesMissed",   totalMisses },
            { "accuracyPct",   System.Math.Round(accuracyPct, 2) },
            { "maxStreak",     maxStreak },
            { "avgReactionMs", System.Math.Round(GetAverageReactionTimeMS(), 2) },
            { "irsScore",      System.Math.Round(CalculateIRS(difficulty), 4) }
        };

        // Desglose por dedos para la App Móvil/Escritorio
        for (int i = 0; i < 5; i++)
        {
            dict.Add("hits_finger_" + i, hitsPerFinger[i]);
            dict.Add("misses_finger_" + i, missesPerFinger[i]);
        }

        return dict;
    }

    private float GetAverageReactionTimeMS()
    {
        if (reactionTimes.Count == 0) return 1500f;
        float sum = 0;
        foreach (float t in reactionTimes) sum += t;
        return (sum / reactionTimes.Count) * 1000f;
    }
}