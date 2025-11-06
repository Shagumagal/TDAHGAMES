using UnityEngine;
using TMPro;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using EasyPeasyFirstPersonController; // tu FPS controller

[Serializable]
public class KV { public string key; public int value; }

[Serializable]
public class RoundSummary
{
    public int round;
    public List<KV> goals;          // metas por id
    public List<KV> delivered;      // entregados por id
    public int errors;
    public float time_used;         // segundos usados en la ronda
    public List<string> deliver_order;
}

[Serializable]
public class GameSummary
{
    public string game_id = "FarmPack";
    public string mode = "objetos+animales";
    public string player_id = "anon";
    public string started_at;       // ISO string
    public string finished_at;      // ISO string
    public int rounds_total;
    public int total_ok;
    public int total_goal;
    public int total_errors;
    public List<RoundSummary> rounds;
    public string notes;
}

public class FarmPackGameController : MonoBehaviour
{
    [Header("Refs")]
    public FirstPersonController fpc;
    public PickUp pickup;
    [Tooltip("Incluye Caja de herramientas y Corrales (cada uno con DropZone).")]
    public DropZone[] dropZones;

    [Header("UI")]
    public GameObject checklistPanel;
    public TMP_Text checklistText;
    public TMP_Text hudTimerText;
    public TMP_Text hudScoreText;

    [Header("Configuración del juego")]
    // Objetivos (objetos)
    public List<string> targetTools  = new() { "pala", "regadera", "hoz" };
    // Objetivos (animales)
    public List<string> targetAnimals = new() { "gallina", "caballo", "gato" };
    // Distractores (objetos)
    public List<string> distractorObjects = new() { "cubo", "circulo" };
    // Distractores (animales)
    public List<string> distractorAnimals = new() { "tigre", "lobo", "pinguino" };

    [Range(1, 3)] public int countPerTool = 1;
    [Range(1, 3)] public int countPerAnimal = 1;
    public int extraDistractorsObjects = 2;
    public int extraDistractorsAnimals = 3;
    public float tiempoListaVisible = 5f;
    public float tiempoRonda = 60f;
    public int rondas = 3;

    [Header("Spawns & Prefabs")]
    public Transform[] spawnPoints;
    public GameObject toolPrefab;    // malla simple para (pala/regadera/hoz)
    public GameObject animalPrefab;  // malla genérica animal
    public GameObject cubePrefab;    // opcional (si null, crea Primitive Cube)
    public GameObject spherePrefab;  // opcional (si null, crea Primitive Sphere)

    [Header("SFX (opcional)")]
    public AudioSource sfxOk, sfxError, sfxBell;

    // ---- Estado de juego ----
    int rondaActual = 0;
    float tRestante;
    float rondaStartTime;
    bool roundClosed = false;

    readonly List<GameObject> spawned = new();
    readonly Dictionary<string, int> goals = new();     // id -> cuantos debo
    readonly Dictionary<string, int> delivered = new(); // id -> entregados
    int errores = 0;
    readonly List<string> ordenEntregas = new();

    // ---- Logging / JSON ----
    readonly List<RoundSummary> roundLogs = new();
    string startedAtIso;
    string lastSavedPath;

    void Start()
    {
        foreach (var z in dropZones)
        {
            z.OnItemAccepted += OnAccepted;
            z.OnItemRejected += OnRejected;
        }
        startedAtIso = DateTime.UtcNow.ToString("o");
        NuevaRonda();
    }

    void NuevaRonda()
    {
        // preparar siguiente ronda
        roundClosed = false;
        LimpiarEscena();
        rondaActual++;
        if (rondaActual > rondas) { FinJuego(); return; }

        goals.Clear(); delivered.Clear(); errores = 0; ordenEntregas.Clear();

        foreach (var id in targetTools)   { goals[id] = countPerTool;   delivered[id] = 0; }
        foreach (var id in targetAnimals) { goals[id] = countPerAnimal; delivered[id] = 0; }

        checklistText.text =
            "Entrega en su lugar:\n" +
            "- Caja: " + string.Join(", ", targetTools.Select(x => $"{x}×{countPerTool}")) + "\n" +
            "- Corrales: " + string.Join(", ", targetAnimals.Select(x => $"{x}×{countPerAnimal}"));

        checklistPanel.SetActive(true);
        fpc.SetControl(false);
        fpc.SetCursorVisibility(true);

        SpawnAll();
        Invoke(nameof(ComenzarRonda), tiempoListaVisible);
    }

    void ComenzarRonda()
    {
        checklistPanel.SetActive(false);
        fpc.SetControl(true);
        fpc.SetCursorVisibility(false);
        tRestante = tiempoRonda;
        rondaStartTime = Time.time;
        if (sfxBell) sfxBell.Play();
    }

    void Update()
    {
        if (rondaActual > rondas) return;
        if (checklistPanel.activeSelf) return;

        tRestante -= Time.deltaTime;
        hudTimerText.text = $"Tiempo: {Mathf.CeilToInt(tRestante)} s";

        int ok = delivered.Values.Sum();
        int goal = goals.Values.Sum();
        hudScoreText.text = $"Ok: {ok}/{goal} · Err: {errores}";

        bool finished = (tRestante <= 0f) || (ok >= goal);
        if (!roundClosed && finished)
        {
            roundClosed = true;              // evita doble guardado
            SaveRoundSummary();              // guarda JSON de la ronda
            fpc.SetControl(false);
            fpc.SetCursorVisibility(false);
            Invoke(nameof(NuevaRonda), 1.0f);
        }
    }

    void OnAccepted(string id)
    {
        if (goals.TryGetValue(id, out var need))
        {
            if (delivered[id] < need)
            {
                delivered[id]++;
                ordenEntregas.Add(id);
                if (sfxOk) sfxOk.Play();
                return;
            }
            // excedente (ya cumplida la cuota de ese id)
            errores++;
            if (sfxError) sfxError.Play();
        }
        else
        {
            // no es objetivo (distractor)
            errores++;
            if (sfxError) sfxError.Play();
        }
    }

    void OnRejected(string _)
    {
        errores++;
        if (sfxError) sfxError.Play();
    }

    void SpawnAll()
    {
        var ids = new List<string>();

        foreach (var id in targetTools)
            for (int i = 0; i < countPerTool; i++) ids.Add(id);

        foreach (var id in targetAnimals)
            for (int i = 0; i < countPerAnimal; i++) ids.Add(id);

        var objD = distractorObjects.ToList();
        for (int i = 0; i < extraDistractorsObjects; i++)
            ids.Add(objD[i % objD.Count]);

        var aniD = distractorAnimals.ToList();
        for (int i = 0; i < extraDistractorsAnimals; i++)
            ids.Add(aniD[i % aniD.Count]);

        ids = ids.OrderBy(_ => UnityEngine.Random.value).ToList();

        int n = Mathf.Min(spawnPoints.Length, ids.Count);
        for (int i = 0; i < n; i++)
            SpawnOne(ids[i], spawnPoints[i].position, spawnPoints[i].rotation);
    }

    void SpawnOne(string id, Vector3 pos, Quaternion rot)
    {
        GameObject go;

        if (id == "cubo")
        {
            go = cubePrefab ? Instantiate(cubePrefab, pos, rot)
                            : GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (!cubePrefab) go.transform.SetPositionAndRotation(pos, rot);
        }
        else if (id == "circulo")
        {
            go = spherePrefab ? Instantiate(spherePrefab, pos, rot)
                              : GameObject.CreatePrimitive(PrimitiveType.Sphere);
            if (!spherePrefab) go.transform.SetPositionAndRotation(pos, rot);
        }
        else if (targetAnimals.Contains(id) || distractorAnimals.Contains(id))
        {
            go = Instantiate(animalPrefab, pos, rot);
        }
        else
        {
            go = Instantiate(toolPrefab, pos, rot);
        }

        go.tag = "Draggable";
        if (!go.GetComponent<Collider>())
            go.AddComponent<BoxCollider>(); // (primitivas ya traen collider)
        if (!go.TryGetComponent<Rigidbody>(out var rb))
            rb = go.AddComponent<Rigidbody>();
        rb.useGravity = true;

        var tag = go.GetComponent<ItemTag>(); if (!tag) tag = go.AddComponent<ItemTag>();
        tag.itemId = id;

        spawned.Add(go);
    }

    void SaveRoundSummary()
    {
        var ok = delivered.Values.Sum();
        var goal = goals.Values.Sum();

        // Diccionarios -> listas serializables
        List<KV> kvGoals = goals.Select(kv => new KV { key = kv.Key, value = kv.Value }).ToList();
        List<KV> kvDelivered = delivered.Select(kv => new KV { key = kv.Key, value = kv.Value }).ToList();

        float timeUsed = Mathf.Max(0f, Time.time - rondaStartTime);

        roundLogs.Add(new RoundSummary
        {
            round = rondaActual,
            goals = kvGoals,
            delivered = kvDelivered,
            errors = errores,
            time_used = timeUsed,
            deliver_order = new List<string>(ordenEntregas)
        });
    }

    void LimpiarEscena()
    {
        foreach (var go in spawned) if (go) Destroy(go);
        spawned.Clear();
        CancelInvoke();
    }

    void FinJuego()
    {
        fpc.SetControl(false);
        fpc.SetCursorVisibility(true);
        checklistPanel.SetActive(true);

        int totalOk = roundLogs.Sum(r => r.delivered.Sum(kv => kv.value));
        int totalGoal = roundLogs.Sum(r => r.goals.Sum(kv => kv.value));
        int totalErr = roundLogs.Sum(r => r.errors);

        // Mostrar en UI
        string orden = string.Join(", ", roundLogs.LastOrDefault()?.deliver_order ?? new List<string>());
        checklistText.text =
            $"¡Listo!\n\nAciertos: {totalOk}/{totalGoal}\nErrores: {totalErr}\nOrden (última ronda): {orden}";

        // Guardar JSON resumen
        SaveGameJson(totalOk, totalGoal, totalErr);
    }

    void SaveGameJson(int totalOk, int totalGoal, int totalErr)
    {
        var summary = new GameSummary
        {
            started_at = startedAtIso,
            finished_at = DateTime.UtcNow.ToString("o"),
            rounds_total = roundLogs.Count,
            total_ok = totalOk,
            total_goal = totalGoal,
            total_errors = totalErr,
            rounds = roundLogs,
            notes = "FarmPack (objetos+animales). Distractores: cubo/circulo + tigre/lobo/pinguino."
        };

        string json = JsonUtility.ToJson(summary, true);
        string folder = Path.Combine(Application.persistentDataPath, "tdah_results");
        Directory.CreateDirectory(folder);
        string file = $"farmpack_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
        string path = Path.Combine(folder, file);
        File.WriteAllText(path, json);
        lastSavedPath = path;

        // (Opcional) Muestra dónde quedó el archivo
        Debug.Log($"[FarmPack] JSON guardado en: {path}\n{json}");
    }
}
