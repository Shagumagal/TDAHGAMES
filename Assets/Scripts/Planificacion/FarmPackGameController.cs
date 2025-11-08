using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class FarmPackGameController : MonoBehaviour
{
    // ========= REFS =========
    [Header("Refs")]
    public EasyPeasyFirstPersonController.FirstPersonController fpc;
    public PickUp pickup;
    public DropZone[] dropZones;

    [Header("UI")]
    public GameObject checklistPanel;    // opcional
    public TMP_Text  checklistText;      // opcional
    public TMP_Text  hudTimerText;       // HUD
    public TMP_Text  hudScoreText;       // HUD
    public TMP_Text  hudInstructionsText;// Texto fijo arriba-derecha (si lo usas)
    public GameObject startPanel;        // Overlay central
    public TMP_Text  startText;          // Texto central

    [Header("Inicio")]
    public bool waitForEnterToStart = false;

    // ========= CONFIG =========
    [Header("Configuración")]
    // Requeridos = ids en targetAnimals (normalmente Dom ∪ NoDom)
    public List<string> targetAnimals = new() { "gallina", "gato", "caballo", "tigre", "pinguino", "lobo" };
    public string[] domesticos   = new[] { "gallina", "gato", "caballo" };
    public string[] noDomesticos = new[] { "tigre", "pinguino", "lobo" };
    [Range(1, 3)] public int countPerAnimal = 1;

    [Tooltip("Duración de la ronda (segundos).")]
    public float tiempoRonda = 120f;
    public int   rondas = 1;

    [Header("Spawns & Prefabs")]
    public Transform[] spawnPoints;
    public GameObject  animalPrefab;
    public GameObject  toolPrefab;
    public GameObject  cubePrefab;
    public GameObject  spherePrefab;

    [Header("SFX (opcional)")]
    public AudioSource sfxOk, sfxError, sfxBell;

    [Header("Cuenta regresiva (opcional)")]
    public bool showCountdown = true;
    public int  countdownFrom = 3;
    public AudioSource voiceIntro;     // arrastra tu MP3 con las instrucciones
    public AudioSource countdownBeep;  // beep por segundo (opcional)

    [Header("Finalización / Persistencia")]
    public bool autoCreateOverlayIfMissing = true;   // crea overlay si falta
    public bool saveJsonAtEnd = true;                // guarda JSON al final
    public bool saveEventsLogInJson = false;         // incluye eventos crudos
    public int  maxErroresParaGanar = int.MaxValue;  // umbral opcional

    // ========= PLANIFICACIÓN POR TAMAÑO =========
    public enum Rule { None, DomBySizeThenNoDomBySize }

    [Header("Reglas de planificación")]
    public Rule rule = Rule.None;

    [Serializable] public struct AnimalSize { public string id; public int rank; } // 1=pequeño … N=grande
    [Tooltip("Asigna un 'rank' por especie. 1=pequeño … N=grande")]
    public AnimalSize[] sizeTable;
    public bool sizeAscending = true; // si false: grande→pequeño

    // ========= LOGGING / MÉTRICAS =========
    [Header("Logging DSM-5")]
    public bool logTrialEvents = true;

    [Serializable] public class TrialEvent {
        public string id;   // "gallina" ...
        public string zone; // nombre de DropZone
        public bool accepted;
        public float t;     // segundos desde inicio de ronda
    }

    [Serializable] public class DSMPlanningMetrics {
        public float firstActionLatency;   // latencia a la 1ª acción
        public float meanDecisionTime;     // tiempo medio entre aciertos consecutivos
        public int   categorySwitches;     // cambios Dom↔NoDom entre aciertos
        public int   longestSameCatRun;    // racha más larga misma categoría
        public float accuracy;             // aciertos / intentos
        public int   attempts, accepted, rejected;
        public float sequenceCompliance;   // 0..1 (cumplimiento de orden)
        public int   sequenceErrors;       // errores de orden
    }

    // ========= ESTADO INTERNO =========
    int   rondaActual = 0;
    float tRestante;
    bool  waitingStart = false;

    readonly List<GameObject> spawned = new();
    readonly Dictionary<string, int> goals     = new(); // id -> debe
    readonly Dictionary<string, int> delivered = new(); // id -> entregó
    int errores = 0;
    readonly List<string> ordenEntregas = new();
    readonly List<TrialEvent> eventsLog = new();
    float tStartRonda;

    // subscripción de lambdas para desuscribir limpio
    readonly Dictionary<DropZone, Action<string>> accHandlers = new();
    readonly Dictionary<DropZone, Action<string>> rejHandlers = new();

    // Secuencia esperada para la regla por tamaño
    List<string> expectedSeq = new();
    int seqPtr = 0, sequenceOk = 0, sequenceErrors = 0;

    // ========= CICLO DE VIDA =========
    void Start()
    {
        // Suscripciones con lambdas (conocemos el nombre de la zona)
        if (dropZones != null)
        {
            foreach (var z in dropZones)
            {
                var zoneName = z.name;
                Action<string> acc = (id) => { OnAccepted(id); LogEvent(id, zoneName, true);  };
                Action<string> rej = (id) => { OnRejected(id); LogEvent(id, zoneName, false); };

                z.OnItemAccepted += acc;
                z.OnItemRejected += rej;

                accHandlers[z] = acc;
                rejHandlers[z] = rej;
            }
        }

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
                if (accHandlers.TryGetValue(z, out var a)) z.OnItemAccepted -= a;
                if (rejHandlers.TryGetValue(z, out var r)) z.OnItemRejected -= r;
            }
        }
    }

    void Update()
    {
        if (waitingStart)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                ComenzarRonda();
            return;
        }

        if (rondaActual > rondas) return;

        tRestante -= Time.deltaTime;
        UpdateHud();

        int ok   = delivered.Values.Sum();
        int goal = goals.Values.Sum();

        if (tRestante <= 0f || ok >= goal)
        {
            if (fpc) fpc.SetControl(false);
            FinJuego();
        }
    }

    // ========= FLUJO =========
    void NuevaRonda()
    {
        LimpiarEscena();

        rondaActual++;
        if (rondaActual > rondas) { FinJuego(); return; }

        goals.Clear(); delivered.Clear(); errores = 0; ordenEntregas.Clear();
        eventsLog.Clear();

        foreach (var id in targetAnimals)
        {
            goals[id] = countPerAnimal;
            delivered[id] = 0;
        }

        // Construir secuencia esperada si hay regla
        BuildExpectedSequence();

        // Instrucciones
        if (hudInstructionsText)
        {
            string dom = string.Join(", ", domesticos.Where(targetAnimals.Contains));
            string nod = string.Join(", ", noDomesticos.Where(targetAnimals.Contains));
            if (rule == Rule.DomBySizeThenNoDomBySize)
                hudInstructionsText.text = $"Orden: Domésticos (pequeño→grande) y luego No domésticos\nDomésticos: {dom}\nNo domésticos: {nod}";
            else
                hudInstructionsText.text = $"Domésticos: {dom}\nNo domésticos: {nod}";
        }
        FixInstructionsAnchor();

        SpawnAll();

        if (fpc) fpc.SetControl(false);

        if (waitForEnterToStart)
        {
            waitingStart = true;
            EnsureStartOverlay();
            if (startPanel) startPanel.SetActive(true);
            if (startText)  startText.text = "Pulsa ENTER para comenzar";
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
        EnsureStartOverlay();
        if (startPanel) startPanel.SetActive(true);
        if (startText)  startText.alignment = TextAlignmentOptions.Center;

        if (voiceIntro && voiceIntro.clip)
        {
            voiceIntro.Play();
            while (voiceIntro.isPlaying) yield return null;
        }

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

        if (startPanel) startPanel.SetActive(false);
        if (fpc) fpc.SetControl(true);

        tStartRonda = Time.time;   // marca inicio de la ronda
        tRestante   = tiempoRonda;
        UpdateHud();
    }

    // ========= EVENTOS DROPZONE =========
    void OnAccepted(string id)
    {
        if (goals.TryGetValue(id, out var need))
        {
            if (delivered[id] < need)
            {
                delivered[id]++;
                ordenEntregas.Add(id);
                if (sfxOk) sfxOk.Play();

                // Cumplimiento de secuencia (si hay regla)
                if (rule == Rule.DomBySizeThenNoDomBySize && expectedSeq.Count > 0)
                {
                    if (seqPtr < expectedSeq.Count && id == expectedSeq[seqPtr]) { seqPtr++; sequenceOk++; }
                    else { sequenceErrors++; }
                }

                UpdateHud();

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

    void LogEvent(string id, string zone, bool accepted)
    {
        if (!logTrialEvents) return;
        eventsLog.Add(new TrialEvent {
            id = id, zone = zone, accepted = accepted, t = Time.time - tStartRonda
        });
    }

    // ========= HUD =========
    void UpdateHud()
    {
        if (hudTimerText) hudTimerText.text = $"Tiempo: {Mathf.CeilToInt(Mathf.Max(0f, tRestante))} s";
        int ok = delivered.Values.Sum();
        int goal = goals.Values.Sum();
        if (hudScoreText) hudScoreText.text = $"Ok: {ok}/{goal} · Err: {errores}";
    }

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

    // ========= SPAWNING =========
    void SpawnAll()
    {
        var ids = new List<string>();
        foreach (var id in targetAnimals)
            for (int i = 0; i < countPerAnimal; i++) ids.Add(id);

        ids = ids.OrderBy(_ => UnityEngine.Random.value).ToList();

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

    // ========= FIN + DSM + JSON =========
    void FinJuego()
    {
        EnsureStartOverlay();
        if (fpc) fpc.SetControl(false);

        int ok   = delivered.Values.Sum();
        int goal = goals.Values.Sum();
        bool timeLeft = tRestante > 0f;
        bool win = (ok >= goal) && timeLeft && (errores <= maxErroresParaGanar);

        var dsm = ComputeDSM(); // métricas (incluye sequenceCompliance)

        if (startPanel) startPanel.SetActive(true);
        if (startText)
        {
            startText.alignment = TextAlignmentOptions.Center;
            startText.enableWordWrapping = true;
            startText.text = win
                ? $"¡Ganaste!\n\nAciertos: {ok}/{goal}\nErrores: {errores}"
                : $"Tiempo agotado\n\nAciertos: {ok}/{goal}\nErrores: {errores}";
        }

        if (saveJsonAtEnd)
        {
            var summary = new RunSummary
            {
                fechaISO = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
                juego   = Application.productName,
                version = Application.version,
                ronda   = rondaActual,
                rondas  = rondas,
                tiempoConfigurado = tiempoRonda,
                tiempoRestante    = Mathf.Max(0f, tRestante),
                aciertos = ok,
                meta     = goal,
                errores  = errores,
                entregados   = new Dictionary<string,int>(delivered),
                ordenEntregas = new List<string>(ordenEntregas),
                win = win,
                dsm = dsm,
                events = saveEventsLogInJson ? new List<TrialEvent>(eventsLog) : null
            };

            string path = SaveSummaryToJson(summary);
            string fileName = Path.GetFileName(path);

            GUIUtility.systemCopyBuffer = path; // ruta completa al portapapeles

            if (startText) startText.text += $"\n\nResultado guardado:\n{fileName}";
            Debug.Log($"[Farm] Resumen guardado en: {path}");
        }

        rondaActual = rondas + 1; // marca fin total
    }

    DSMPlanningMetrics ComputeDSM()
    {
        var acc = eventsLog.Where(e => e.accepted).ToList();
        var rej = eventsLog.Where(e => !e.accepted).ToList();
        int attempts = eventsLog.Count;

        float firstLatency = eventsLog.Count > 0 ? eventsLog[0].t : -1f;

        float meanDT = 0f;
        if (acc.Count > 1) {
            float sum = 0f;
            for (int i = 1; i < acc.Count; i++) sum += acc[i].t - acc[i-1].t;
            meanDT = sum / (acc.Count - 1);
        }

        // Cambios de categoría entre aciertos
        int switches = 0;
        for (int i = 1; i < acc.Count; i++)
            if (Cat(acc[i].id) != Cat(acc[i-1].id)) switches++;

        // Racha más larga de misma categoría
        int longest = 0, curr = 0; string last = null;
        foreach (var e in acc) {
            var c = Cat(e.id);
            if (c == last) curr++; else { curr = 1; last = c; }
            if (curr > longest) longest = curr;
        }

        float accuracy = acc.Count / Mathf.Max(1f, (float)attempts);

        // Cumplimiento de secuencia por tamaño
        float compliance = 0f;
        if (rule == Rule.DomBySizeThenNoDomBySize && expectedSeq.Count > 0)
            compliance = sequenceOk / Mathf.Max(1f, (float)expectedSeq.Count);

        return new DSMPlanningMetrics {
            firstActionLatency = firstLatency,
            meanDecisionTime   = meanDT,
            categorySwitches   = switches,
            longestSameCatRun  = longest,
            accuracy           = accuracy,
            attempts = attempts,
            accepted = acc.Count,
            rejected = rej.Count,
            sequenceCompliance = compliance,
            sequenceErrors     = sequenceErrors
        };
    }

    // ========= HELPERS =========
    string Cat(string id) => domesticos.Contains(id) ? "dom" : "nodom";

    int Rank(string id)
    {
        for (int i = 0; i < sizeTable.Length; i++)
            if (sizeTable[i].id == id) return sizeTable[i].rank;
        return 999; // si no está definido, va al final
    }

    void BuildExpectedSequence()
    {
        expectedSeq.Clear(); seqPtr = 0; sequenceOk = 0; sequenceErrors = 0;

        if (rule != Rule.DomBySizeThenNoDomBySize) return;

        // 1) Domésticos presentes
        var dom = domesticos.Where(targetAnimals.Contains).ToList();
        dom.Sort((a,b) => Rank(a).CompareTo(Rank(b)));
        if (!sizeAscending) dom.Reverse();
        foreach (var id in dom) for (int i=0;i<countPerAnimal;i++) expectedSeq.Add(id);

        // 2) No domésticos presentes
        var nod = noDomesticos.Where(targetAnimals.Contains).ToList();
        nod.Sort((a,b) => Rank(a).CompareTo(Rank(b)));
        if (!sizeAscending) nod.Reverse();
        foreach (var id in nod) for (int i=0;i<countPerAnimal;i++) expectedSeq.Add(id);
    }

    [Serializable]
    public class RunSummary
    {
        public string fechaISO;
        public string juego;
        public string version;
        public int ronda, rondas;
        public float tiempoConfigurado, tiempoRestante;
        public int aciertos, meta, errores;
        public Dictionary<string,int> entregados;
        public List<string> ordenEntregas;
        public bool win;
        public DSMPlanningMetrics dsm;
        public List<TrialEvent> events; // opcional (controlado por saveEventsLogInJson)
    }

    string SaveSummaryToJson(RunSummary data)
    {
        string dir = Path.Combine(Application.persistentDataPath, "Resultados");
        Directory.CreateDirectory(dir);
        string file = Path.Combine(dir, $"planificacion_{DateTime.Now:yyyyMMdd_HHmmss}.json");
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(file, json, Encoding.UTF8);
        return file;
    }

    // Crea un overlay simple si falta (tamaño/márgenes agradables)
    void EnsureStartOverlay()
    {
        if (!autoCreateOverlayIfMissing) return;
        if (startPanel && startText) return;

        var canvas = FindObjectOfType<Canvas>();
        if (!canvas)
        {
            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var c  = go.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay;
            var s  = go.GetComponent<CanvasScaler>(); s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(1920, 1080);
            canvas = c;
            if (!FindObjectOfType<EventSystem>())
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        var panelGO = new GameObject("AutoStartOverlay", typeof(RectTransform), typeof(Image));
        panelGO.transform.SetParent(canvas.transform, false);
        startPanel = panelGO;
        var rt = (RectTransform)panelGO.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(820, 260);
        var img = panelGO.GetComponent<Image>();
        img.color = new Color(0, 0, 0, 0.65f);

        var textGO = new GameObject("StartText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGO.transform.SetParent(panelGO.transform, false);
        startText = textGO.GetComponent<TextMeshProUGUI>();
        startText.alignment = TextAlignmentOptions.Center;
        startText.fontSize  = 34;
        startText.color     = Color.white;
        startText.enableWordWrapping = true;
        startText.margin = new Vector4(24, 20, 24, 20);
    }
}
