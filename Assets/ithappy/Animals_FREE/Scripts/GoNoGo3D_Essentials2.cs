using UnityEngine;
using TMPro;
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

// << Nuevo Input System: usar solo si está habilitado >>
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TDAHGame
{
  public class GoNoGo3D_Essentials2 : MonoBehaviour
  {
    // ------------------ DATA TYPES ------------------
    [Serializable]
    public class TrialEvent
    {
      public int trial_id, block_index, prefab_index, stim_onset_ms, stim_duration_ms;
      public string trial_type;        // "go"|"nogo"
      public bool responded, correct, commission_error, omission_error, rt_valid;
      public int response_time_ms;     // -1 si no respondió
    }

    [Serializable]
    public class SessionSummary
    {
      public string session_id, started_at_utc, ended_at_utc;
      public int blocks, trials_per_block, n_trials, go_trials, nogo_trials;
      public double commission_rate, omission_rate, rt_cv, fast_guess_rate, lapses_rate, vigilance_decrement, valid_trial_ratio;
      public int rt_median_ms;
    }

    [Serializable] private class SessionFile { public SessionSummary summary; public List<TrialEvent> trials; }

    // ---- Config por bloque ----
    [Serializable]
    public class BlockSettings
    {
      [Tooltip("Prefabs Go para este bloque (1..N)")] public GameObject[] goPrefabs;
      [Tooltip("Prefabs No-Go para este bloque (1..M)")] public GameObject[] noGoPrefabs;
      [Range(0f, 1f)] public float goRatio = 0.8f;     // 4:1 típico
      [Tooltip("Duración del estímulo (ms)")] public float stimMs = 1200f;
      [Tooltip("Máx. repeticiones seguidas del mismo tipo")] public int maxSameTypeRun = 2;
      [Tooltip("Máx. repeticiones del mismo prefab")] public int maxSamePrefabRun = 2;
      [TextArea] public string ruleHint;
    }

    // ------------------ REFERENCES ------------------
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI infoText;
    // --- Inicio limpio / feedback ---
    [SerializeField] private KeyCode startKey = KeyCode.Return; // ENTER para iniciar (puedes poner Space si quieres)
    [SerializeField] private int readyPauseMs = 800;            // pausa previa al 1er estímulo
    private bool feedbackEnabled = false;                       // bloquea correct/error hasta que empiece la tarea

    [Header("Anchors")]
    [SerializeField] private Transform stimAnchor;          // Empty en (0,~0.2,0)
    [SerializeField] private Transform platform;            // opcional: plataforma con collider

    [Header("Bloques (define 3)")]
    [SerializeField] private int trialsPerBlock = 60;
    [SerializeField] private List<BlockSettings> blocksSettings = new List<BlockSettings>(); // mete 3 elementos

    [Header("Orientación fija (perfil)")]
    [SerializeField] private float goYawDeg = 90f;
    [SerializeField] private float noGoYawDeg = 90f;

    [Header("Timing (ms)")]
    [SerializeField] private float postWindowMs = 300f;
    [SerializeField] private float itiMinMs = 900f;
    [SerializeField] private float itiMaxMs = 1400f;

    [Header("Randomization")]
    [Tooltip("0 = aleatorio por sesión; >0 = determinista")]
    [SerializeField] private int randomSeed = 0;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip sfxClick, sfxCorrect, sfxError;

    [Header("Guardar JSON local")]
    [SerializeField] private bool saveJsonLocally = true;
    [SerializeField] private string jsonFolderName = "sessions";

    [Header("Countdown SFX (opcional)")]
    [SerializeField] private AudioClip sfxTick;
    [SerializeField] private AudioClip sfxFinal;

    [Header("Ground snap")]
    [SerializeField] private LayerMask groundMask;       // capa(s) del suelo/plataforma
    [SerializeField] private float snapRayHeight = 5f;   // altura desde donde lanzo ray hacia abajo
    [SerializeField] private float snapExtraOffset = 0f; // ajuste fino (+ sube, - baja)

    // ------------------ RUNTIME ------------------
    private readonly List<TrialEvent> trials = new List<TrialEvent>();
    private readonly System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
    private bool running = false;

    // plan (tipo, prefabIndex, blockIndex)
    private readonly List<(string type, int prefabIndex, int block)> plan = new List<(string, int, int)>();

    void Start()
    {
      if (infoText)
        infoText.text = "Regla:\nPRESIONA ESPACIO (o clic/touch) con la gallina AMIGA (Go).\nNO presiones con la gallina PROHIBIDA (No-Go).\n\nPulsa ESPACIO para empezar.";
    }

    void Update()
    {
      if (!running && Input.GetKeyDown(startKey))
      {
        PreparePlan();
        StartCoroutine(RunSessionFromPlan());
      }
    }

    // ------------------ PLAN ------------------
    private void PreparePlan()
    {
      plan.Clear();
      int seed = (randomSeed == 0) ? Environment.TickCount : randomSeed;
      System.Random rng = new System.Random(seed);

      int blocks = Mathf.Max(1, blocksSettings.Count);
      for (int b = 1; b <= blocks; b++)
      {
        var cfg = blocksSettings[b - 1];
        int n = Mathf.Max(1, trialsPerBlock);
        int targetGo = Mathf.RoundToInt(n * Mathf.Clamp01(cfg.goRatio));
        int targetNoGo = n - targetGo;

        var types = new List<string>();
        types.AddRange(Enumerable.Repeat("go", targetGo));
        types.AddRange(Enumerable.Repeat("nogo", targetNoGo));

        // shuffle hasta cumplir run-length
        List<string> seq = null;
        for (int attempts = 0; attempts < 80; attempts++)
        {
          var tmp = new List<string>(types);
          for (int i = tmp.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (tmp[i], tmp[j]) = (tmp[j], tmp[i]); }
          if (TypeRunOK(tmp, cfg.maxSameTypeRun)) { seq = tmp; break; }
        }
        if (seq == null) seq = types; // fallback

        // bolsas de prefabs
        Queue<int> goBag = MakeBag(CountSafe(cfg.goPrefabs), rng);
        Queue<int> noGoBag = MakeBag(CountSafe(cfg.noGoPrefabs), rng);

        int lastPrefab = -1, runPrefab = 0;
        foreach (var t in seq)
        {
          bool isGo = (t == "go");
          int idx = isGo ? NextFromBag(ref goBag, CountSafe(cfg.goPrefabs), rng)
                         : NextFromBag(ref noGoBag, CountSafe(cfg.noGoPrefabs), rng);

          if (idx == lastPrefab) runPrefab++; else runPrefab = 1;
          if (runPrefab > cfg.maxSamePrefabRun)
          {
            int newIdx = idx;
            for (int tries = 0; tries < 10 && newIdx == idx; tries++)
              newIdx = rng.Next(isGo ? CountSafe(cfg.goPrefabs) : CountSafe(cfg.noGoPrefabs));
            idx = newIdx; runPrefab = 1;
          }
          lastPrefab = idx;

          plan.Add((t, idx, b));
        }
      }
    }

    private bool TypeRunOK(List<string> seq, int maxRun)
    {
      string last = ""; int run = 0;
      foreach (var t in seq) { if (t == last) run++; else { last = t; run = 1; } if (run > maxRun) return false; }
      return true;
    }
    private int CountSafe(GameObject[] arr) => (arr == null || arr.Length == 0) ? 1 : arr.Length;
    private Queue<int> MakeBag(int count, System.Random rng)
    {
      var list = new List<int>(); for (int i = 0; i < count; i++) list.Add(i);
      for (int i = list.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (list[i], list[j]) = (list[j], list[i]); }
      return new Queue<int>(list);
    }
    private int NextFromBag(ref Queue<int> bag, int count, System.Random rng)
    {
      if (bag == null || bag.Count == 0) bag = MakeBag(count, rng);
      return bag.Dequeue();
    }

    // ------------------ SESSION ------------------
    private IEnumerator RunSessionFromPlan()
    {
      running = true; trials.Clear();
      if (infoText) infoText.text = "";

      string sessionId = $"S_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
      string startedIso = DateTime.UtcNow.ToString("o");

      sw.Reset(); sw.Start();
      if (CountdownOverlay.Instance != null)
        yield return CountdownOverlay.Instance.Run(5, "¡Prepárate!", sfxTick, sfxFinal);

      // Cuenta atrás previa y habilitar feedback
      yield return new WaitForSeconds(readyPauseMs / 1000f);
      feedbackEnabled = true;

      int trialId = 0;
      int currentBlock = -1;

      foreach (var p in plan)
      {
        // ------ Mostrar instrucciones al CAMBIAR de bloque ------
        if (p.block != currentBlock)
        {
          currentBlock = p.block;
          yield return ShowBlockIntro(currentBlock);
          if (CountdownOverlay.Instance != null)
            yield return CountdownOverlay.Instance.Run(3, "Listo para el Bloque " + currentBlock, sfxTick, sfxFinal);
          yield return new WaitForSeconds(0.6f);      // pausa corta opcional
        }
        // --------------------------------------------------------

        var cfg = blocksSettings[p.block - 1];
        bool isGo = p.type == "go";
        var prefArr = isGo ? cfg.goPrefabs : cfg.noGoPrefabs;
        GameObject prefab = (prefArr != null && prefArr.Length > 0)
                            ? prefArr[Mathf.Clamp(p.prefabIndex, 0, prefArr.Length - 1)]
                            : null;

        // Instancia estímulo
        var stim = (prefab != null) ? Instantiate(prefab, stimAnchor.position, Quaternion.identity)
                                    : new GameObject("DummyStim");

        // Desactivar control/movimiento del pack que pueda separarlo del suelo
        var cc   = stim.GetComponent<CharacterController>(); if (cc) cc.enabled = false;
        var anim = stim.GetComponent<Animator>(); if (anim) anim.applyRootMotion = false;
        var mover = stim.GetComponent("CreatureMover") as Behaviour; if (mover) mover.enabled = false;
        var input = stim.GetComponent("MovePlayerInput") as Behaviour; if (input) input.enabled = false;

        // Apoyar en suelo/plataforma
        PlaceOnGround(stim.transform);

        // Orientación fija
        stim.transform.rotation = Quaternion.Euler(0f, isGo ? goYawDeg : noGoYawDeg, 0f);

        var te = new TrialEvent
        {
          trial_id = ++trialId,
          block_index = p.block,
          trial_type = p.type,
          prefab_index = p.prefabIndex,
          stim_onset_ms = (int)sw.ElapsedMilliseconds,
          stim_duration_ms = Mathf.RoundToInt(cfg.stimMs)
        };

        // Ventana de respuesta
        float t0 = Time.time;
        bool responded = false; int rt = -1;

        while ((Time.time - t0) * 1000f < cfg.stimMs)
        {
          if (Pressed()) { responded = true; rt = (int)((Time.time - t0) * 1000f); Play(sfxClick); break; }
          yield return null;
        }

        Destroy(stim);

        // Post-window
        if (!responded && postWindowMs > 0f)
        {
          float p0 = Time.time;
          while ((Time.time - p0) * 1000f < postWindowMs)
          {
            if (Pressed()) { responded = true; rt = (int)(cfg.stimMs + (Time.time - p0) * 1000f); Play(sfxClick); break; }
            yield return null;
          }
        }

        // Evaluación
        bool correct = isGo ? responded : !responded;
        bool commission = (!isGo && responded);
        bool omission = (isGo && !responded);
        bool rtValid = isGo && responded && rt >= 150 && rt <= 2000;

        if (correct) Play(sfxCorrect); else Play(sfxError);

        te.responded = responded; te.response_time_ms = rt; te.correct = correct;
        te.commission_error = commission; te.omission_error = omission; te.rt_valid = rtValid;
        trials.Add(te);

        // ITI
        float iti = UnityEngine.Random.Range(itiMinMs, itiMaxMs) / 1000f;
        yield return new WaitForSeconds(iti);
      }

      sw.Stop();
      string endedIso = DateTime.UtcNow.ToString("o");

      var summary = ComputeSummary(trials);
      summary.session_id = sessionId; summary.started_at_utc = startedIso; summary.ended_at_utc = endedIso;
      summary.blocks = Mathf.Max(1, blocksSettings.Count); summary.trials_per_block = trialsPerBlock;
      summary.n_trials = trials.Count;
      summary.go_trials = trials.Count(t => t.trial_type == "go");
      summary.nogo_trials = trials.Count(t => t.trial_type == "nogo");

      if (saveJsonLocally) SaveJson(sessionId, summary, trials);

      if (infoText)
      {
        infoText.text =
          $"Fin\nComisión: {summary.commission_rate:P0} | Omisión: {summary.omission_rate:P0}\n" +
          $"RT mediana: {summary.rt_median_ms} ms | CV RT: {summary.rt_cv:0.00}\n" +
          $"Adivinazos: {summary.fast_guess_rate:P0} | Lapsos: {summary.lapses_rate:P0}\n" +
          $"Vigilancia Δ: {summary.vigilance_decrement:+0.00;-0.00;0.00}";
      }

      running = false;
    }

    // ---- Input helper (mouse/tecla/táctil) ----
    private bool Pressed()
    {
#if ENABLE_INPUT_SYSTEM
      if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) return true;
      if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
      if (Touchscreen.current != null)
      {
        foreach (var t in Touchscreen.current.touches)
        {
          if (t.press.wasPressedThisFrame) return true;
        }
      }
#endif
      // Input antiguo
      if (Input.GetKeyDown(KeyCode.Space)) return true;
      if (Input.GetMouseButtonDown(0)) return true;
      if (Input.touchCount > 0 && Input.GetTouch(0).phase == UnityEngine.TouchPhase.Began) return true;
      return false;
    }

    private IEnumerator ShowBlockIntro(int blockIndex)
    {
      // Si no existe StartUIPanel, no bloquees el flujo
      if (StartUIPanel.Instance == null) yield break;

      var cfg = blocksSettings[Mathf.Clamp(blockIndex - 1, 0, blocksSettings.Count - 1)];
      string title = $"Bloque {blockIndex} de {blocksSettings.Count}";
      string body = string.IsNullOrWhiteSpace(cfg.ruleHint)
                      ? "Sigue las mismas reglas.\n\nPulsa ENTER para continuar."
                      : cfg.ruleHint + "\n\nPulsa ENTER para continuar.";

      bool go = false;
      StartUIPanel.Instance.Show(title, body, () => go = true);
      while (!go) yield return null;   // espera Enter o botón
    }

    // ---- Snap al suelo/plataforma ----
    private void PlaceOnGround(Transform t)
    {
      if (!t) return;

      // 1) Raycast hacia abajo
      Vector3 start = t.position + Vector3.up * snapRayHeight;
      if (Physics.Raycast(start, Vector3.down, out RaycastHit hit, snapRayHeight * 2f, groundMask))
      {
        SnapBottomToY(t, hit.point.y + snapExtraOffset);
        return;
      }

      // 2) Fallback: plataforma o anchor
      float yRef = GetPlatformTopY() + snapExtraOffset;
      SnapBottomToY(t, yRef);
    }

    private void SnapBottomToY(Transform t, float targetY)
    {
      var r = t.GetComponentInChildren<Renderer>();
      if (!r)
      {
        var p = t.position; p.y = targetY; t.position = p;
        return;
      }
      float bottomNow = r.bounds.center.y - r.bounds.extents.y; // base actual
      float delta = targetY - bottomNow;
      t.position += new Vector3(0f, delta, 0f);
    }

    private float GetPlatformTopY()
    {
      if (!platform) return stimAnchor ? stimAnchor.position.y : 0f;
      var col = platform.GetComponent<Collider>();
      return col ? col.bounds.max.y : platform.position.y;
    }

    // ---- Métricas ----
    private SessionSummary ComputeSummary(List<TrialEvent> list)
    {
      var s = new SessionSummary();
      var go = list.Where(t => t.trial_type == "go").ToList();
      var nogo = list.Where(t => t.trial_type == "nogo").ToList();
      int com = nogo.Count(t => t.responded), omi = go.Count(t => !t.responded);
      s.commission_rate = SafeDiv(com, Math.Max(1, nogo.Count));
      s.omission_rate = SafeDiv(omi, Math.Max(1, go.Count));
      var RT = go.Where(t => t.rt_valid).Select(t => t.response_time_ms).ToList();
      s.rt_median_ms = (RT.Count > 0) ? Median(RT) : 0;
      s.rt_cv = (RT.Count > 1) ? StdDev(RT) / RT.Average() : 0.0;
      s.fast_guess_rate = SafeDiv(go.Count(t => t.responded && t.response_time_ms >= 0 && t.response_time_ms < 150), Math.Max(1, go.Count));
      s.lapses_rate = SafeDiv(go.Count(t => (!t.responded) || (t.response_time_ms > 1200)), Math.Max(1, go.Count));
      s.vigilance_decrement = Vigilance(go);
      int validTrials = list.Count(t => t.trial_type == "nogo" || (t.trial_type == "go" && t.response_time_ms >= 0));
      s.valid_trial_ratio = SafeDiv(validTrials, Math.Max(1, list.Count));
      return s;
    }
    private double Vigilance(List<TrialEvent> go)
    {
      int n = go.Count; if (n < 9) return 0;
      int t = n / 3;
      double Acc(List<TrialEvent> c) { int ok = c.Count(e => e.responded && e.correct); return SafeDiv(ok, Math.Max(1, c.Count)); }
      return Acc(go.Take(t).ToList()) - Acc(go.Skip(2 * t).ToList());
    }
    private double SafeDiv(int a, int b) => (b <= 0) ? 0.0 : (double)a / b;
    private int Median(List<int> x) { x.Sort(); int n = x.Count; return n == 0 ? 0 : (n % 2 == 1 ? x[n / 2] : (x[n / 2 - 1] + x[n / 2]) / 2); }
    private double StdDev(List<int> x) { double m = x.Average(), v = 0; foreach (var v0 in x) v += (v0 - m) * (v0 - m); v /= Math.Max(1, x.Count - 1); return Math.Sqrt(v); }

    // ---- Guardado JSON ----
    private void SaveJson(string sessionId, SessionSummary summary, List<TrialEvent> trials)
    {
      try
      {
        string basePath = Path.Combine(Application.persistentDataPath, jsonFolderName);
        if (!Directory.Exists(basePath)) Directory.CreateDirectory(basePath);
        var payload = new SessionFile { summary = summary, trials = trials };
        string json = JsonUtility.ToJson(payload, true);
        string path = Path.Combine(basePath, $"{sessionId}.json");
        File.WriteAllText(path, json, System.Text.Encoding.UTF8);
#if UNITY_EDITOR
        Debug.Log($"[GoNoGo] Saved JSON: {path}");
#endif
      }
      catch (Exception ex)
      {
        Debug.LogWarning($"[GoNoGo] Save JSON failed: {ex.Message}");
      }
    }

    // ---- Audio ----
    private void Play(AudioClip clip)
    {
      if (!audioSource || !clip) return;
      audioSource.PlayOneShot(clip);
    }
  }
}
