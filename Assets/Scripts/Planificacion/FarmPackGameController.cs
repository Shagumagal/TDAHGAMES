using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class FarmPackGameController : MonoBehaviour
{
    [Header("Refs")]
    public EasyPeasyFirstPersonController.FirstPersonController fpc;
    public PickUp pickup;
    public DropZone[] dropZones;

    [Header("UI")]
    public GameObject checklistPanel;   // compatibilidad, pero se mantiene apagado
    public TMP_Text checklistText;
    public TMP_Text hudTimerText;       // arriba-izquierda
    public TMP_Text hudScoreText;       // arriba-izquierda
    public TMP_Text hudInstructionsText;// arriba-derecha (fijo)
    public GameObject startPanel;       // overlay central
    public TMP_Text startText;          // texto central

    [Header("Inicio")]
    public bool waitForEnterToStart = false;

    [Header("Configuración")]
    // Animales de la prueba (clasificación)
    public List<string> targetAnimals = new() { "gallina", "gato", "caballo", "tigre", "pinguino", "lobo" };
    public string[] domesticos   = new[] { "gallina", "gato", "caballo" };
    public string[] noDomesticos = new[] { "tigre", "pinguino", "lobo" };
    [Range(1, 3)] public int countPerAnimal = 1;

    [Tooltip("Duración de la ronda (segundos).")]
    public float tiempoRonda = 120f;
    public int rondas = 1;

    [Header("Spawns & Prefabs")]
    public Transform[] spawnPoints;
    public GameObject animalPrefab;
    public GameObject toolPrefab;
    public GameObject cubePrefab;
    public GameObject spherePrefab;

    [Header("SFX (opcional)")]
    public AudioSource sfxOk, sfxError, sfxBell;

    [Header("Cuenta regresiva")]
    public bool showCountdown = true;
    public int countdownFrom = 3;
    public AudioSource voiceIntro;     // arrastra tu mp3 aquí
    public AudioSource countdownBeep;  // opcional por segundo

    // ----- Estado -----
    int rondaActual = 0;
    float tRestante;
    bool waitingStart = false;

    readonly List<GameObject> spawned = new();
    readonly Dictionary<string, int> goals = new();     // id -> debe
    readonly Dictionary<string, int> delivered = new(); // id -> entregado
    int errores = 0;
    readonly List<string> ordenEntregas = new();

    void Start()
    {
        if (dropZones != null)
        {
            foreach (var z in dropZones)
            {
                z.OnItemAccepted += OnAccepted;
                z.OnItemRejected += OnRejected;
            }
        }

        // Que el checklist no tape, y fija la HUD de instrucciones
        if (checklistPanel) checklistPanel.SetActive(false);
        FixInstructionsAnchor();

        NuevaRonda();
    }

    void OnDestroy()
    {
        if (dropZones != null)
        {
            foreach (var z in dropZones)
            {
                z.OnItemAccepted -= OnAccepted;
                z.OnItemRejected -= OnRejected;
            }
        }
    }

    void NuevaRonda()
    {
        LimpiarEscena();
        rondaActual++;
        if (rondaActual > rondas) { FinJuego(); return; }

        goals.Clear(); delivered.Clear(); errores = 0; ordenEntregas.Clear();

        foreach (var id in targetAnimals)
        {
            goals[id] = countPerAnimal;
            delivered[id] = 0;
        }

        // Instrucciones fijas (arriba-derecha)
        if (hudInstructionsText)
        {
            string dom = string.Join(", ", domesticos.Where(targetAnimals.Contains));
            string nod = string.Join(", ", noDomesticos.Where(targetAnimals.Contains));
            hudInstructionsText.text = $"Domésticos: {dom}\nNo domésticos: {nod}";
        }

        if (checklistPanel) checklistPanel.SetActive(false);
        FixInstructionsAnchor();

        SpawnAll();

        if (fpc) fpc.SetControl(false);

        if (waitForEnterToStart)
        {
            waitingStart = true;
            if (startPanel) startPanel.SetActive(true);
            if (startText) startText.text = "Pulsa ENTER para comenzar";
        }
        else
        {
            ComenzarRonda();
        }
    }

    void ComenzarRonda()
    {
        waitingStart = false;
        StopAllCoroutines();
        StartCoroutine(CountdownAndStart());
    }

    IEnumerator CountdownAndStart()
    {
        // Overlay visible y centrado
        if (startPanel) startPanel.SetActive(true);
        if (startText)  startText.alignment = TextAlignmentOptions.Center;

        // 1) Reproducir voz de instrucciones (si hay)
        if (voiceIntro && voiceIntro.clip)
        {
            voiceIntro.Play();
            while (voiceIntro.isPlaying) yield return null;
        }

        // 2) 3-2-1 en pantalla
        if (showCountdown && startText)
        {
            for (int i = Mathf.Max(1, countdownFrom); i >= 1; i--)
            {
                startText.text = i.ToString();
                if (countdownBeep) countdownBeep.Play();
                yield return new WaitForSeconds(1f);
            }
            startText.text = "¡Comienza!";
            if (sfxBell) sfxBell.Play();
            yield return new WaitForSeconds(0.35f);
        }

        // 3) Arrancar la ronda
        if (startPanel) startPanel.SetActive(false);
        if (fpc) fpc.SetControl(true);
        tRestante = tiempoRonda;
        UpdateHud();
    }

    void Update()
    {
        if (waitingStart)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                ComenzarRonda();
            return; // aún no corre el tiempo
        }

        if (rondaActual > rondas) return;

        tRestante -= Time.deltaTime;
        UpdateHud();

        int ok = delivered.Values.Sum();
        int goal = goals.Values.Sum();

        if (tRestante <= 0f || ok >= goal)
        {
            if (fpc) fpc.SetControl(false);
            FinJuego();
        }
    }

    void UpdateHud()
    {
        if (hudTimerText) hudTimerText.text = $"Tiempo: {Mathf.CeilToInt(Mathf.Max(0f, tRestante))} s";
        int ok = delivered.Values.Sum();
        int goal = goals.Values.Sum();
        if (hudScoreText) hudScoreText.text = $"Ok: {ok}/{goal} · Err: {errores}";
        // HUD de tiempo/score ya contemplado en tu versión previa :contentReference[oaicite:7]{index=7}
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
                UpdateHud();

                // Fin anticipado si completa todo
                if (delivered.Values.Sum() >= goals.Values.Sum())
                {
                    if (fpc) fpc.SetControl(false);
                    FinJuego();
                }
                return;
            }
        }
        errores++;
        if (sfxError) sfxError.Play();
        UpdateHud();
    }

    void OnRejected(string _)
    {
        errores++;
        if (sfxError) sfxError.Play();
        UpdateHud();
    }

    void SpawnAll()
    {
        var ids = new List<string>();
        foreach (var id in targetAnimals)
            for (int i = 0; i < countPerAnimal; i++) ids.Add(id);

        ids = ids.OrderBy(_ => Random.value).ToList();

        int n = Mathf.Min(spawnPoints != null ? spawnPoints.Length : 0, ids.Count);
        for (int i = 0; i < n; i++)
            SpawnOne(ids[i], spawnPoints[i].position, spawnPoints[i].rotation);
    }

    void SpawnOne(string id, Vector3 pos, Quaternion rot)
    {
        GameObject go;
        if (animalPrefab) go = Instantiate(animalPrefab, pos, rot);
        else if (spherePrefab) go = Instantiate(spherePrefab, pos, rot);
        else { go = GameObject.CreatePrimitive(PrimitiveType.Capsule); go.transform.SetPositionAndRotation(pos, rot); }

        go.tag = "Draggable";
        if (!go.GetComponent<Collider>()) go.AddComponent<BoxCollider>();
        if (!go.TryGetComponent<Rigidbody>(out var rb)) rb = go.AddComponent<Rigidbody>();
        rb.useGravity = true;

        var tag = go.GetComponent<ItemTag>(); if (!tag) tag = go.AddComponent<ItemTag>();
        tag.itemId = id;

        spawned.Add(go);
    }

    void LimpiarEscena()
    {
        foreach (var go in spawned) if (go) Destroy(go);
        spawned.Clear();
        CancelInvoke();
    }

    void FinJuego()
    {
        if (startPanel) startPanel.SetActive(true);
        if (startText)
        {
            int ok = delivered.Values.Sum();
            int goal = goals.Values.Sum();
            startText.alignment = TextAlignmentOptions.Center;
            startText.text = $"¡Listo!\n\nAciertos: {ok}/{goal}\nErrores: {errores}";
        }
        rondaActual = rondas + 1;
    }

    // --- FIX duro para que nunca se quede en el centro ---
    void FixInstructionsAnchor()
    {
        if (!hudInstructionsText) return;
        var rt = hudInstructionsText.rectTransform;
        rt.anchorMin = new Vector2(1, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot     = new Vector2(1, 1);
        rt.anchoredPosition = new Vector2(-16f, -16f);
        hudInstructionsText.alignment = TextAlignmentOptions.TopRight;
        hudInstructionsText.color = Color.black;
        hudInstructionsText.raycastTarget = false;
    }
}
