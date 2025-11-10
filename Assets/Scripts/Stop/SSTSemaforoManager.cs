using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// CSST-ready Stop-Signal (Semáforo).
/// Mantener ESPACIO en VERDE (Go). Soltar en ROJO (Stop).
/// Si csstMode=true: agrega fase de CUE 200ms (Certain vs Uncertain) para medir control proactivo.
/// Exporta TSV (con "n/a") y CSV (vacío) con columnas: onset,duration,trialid,cue_onset,gng_id,cue_id,corr_resp,response,correctness,rt
/// </summary>
public class SSTSemaforoManager : MonoBehaviour
{
    /* -------------------- UI -------------------- */
    [Header("UI")]
    public SSTTimerHUD timerHUD;          // arrástralo (opcional; se auto-busca)
    public StartUIPanel startUI;          // (opcional)
    public int countdownSeconds = 3;
    public AudioClip tickSfx;             // (opcional)
    public AudioClip finalSfx;            // (opcional)

    /* -------------------- Refs juego -------------------- */
    [Header("Refs")]
    public SSTRunner runner;              // jugador (tiene moveKey)
    public AudioSource stopBeep;          // sonido de Stop
    public SSTLightCue lightCue;          // overlay verde/rojo (CanvasGroups)
    public GameObject luzVerde;           // meshes (opcional)
    public GameObject luzRoja;

    /* -------------------- Diseño clásico (SST simple) -------------------- */
    [Header("SST simple")]
    public int blocks = 2;
    public int trialsPerBlock = 24;
    [Range(0f,1f)] public float stopProportion = 0.25f;

    /* -------------------- CSST (proactivo vs reactivo) -------------------- */
    [Header("CSST")]
    public bool csstMode = false;         // habilita cues (Certain vs Uncertain)
    public int runs = 2;                  // # de runs CSST
    public int perRunCertainGo = 32;      // 32
    public int perRunUncertainGo = 32;    // 32
    public int perRunStop = 16;           // 16 (Uncertain-Stop)
    public int cueDurationMs = 200;       // 200 ms
    // Nota visual: usamos GREEN para "Certain" y RED tenue como "Uncertain" (puedes cambiar sprite del rojo a blanco si prefieres)

    /* -------------------- Timing -------------------- */
    [Header("Timing")]
    public int stimDurationMs = 3000;
    public float itiMin = 1.0f;           // Para CSST pon 1..4 s
    public float itiMax = 1.5f;

    /* -------------------- Staircase SSD -------------------- */
    [Header("SSD Staircase")]
    public int ssdStartMs = 250;          // Para CSST pon 200
    public int ssdStepMs = 50;            // ±50
    public int ssdMinMs = 50;
    public int ssdMaxMs = 700;

    /* -------------------- Criterios de respuesta -------------------- */
    [Header("Criterios de respuesta")]
    public float moveSpeedThreshold = 0.10f;
    public int rtMinMs = 150;

    [Header("STOP éxito")]
    public int stopSuccessWindowMs = 800; // ventana para soltar tras beep
    public int stopHoldMs = 120;          // mantener suelto
    public bool requireSpeedBelowOnStop = false;

    /* -------------------- Export -------------------- */
    public enum MissingPolicy { Blank, NaN, NA } // NA => "n/a"
    [Header("Export")]
    public MissingPolicy tsvMissing = MissingPolicy.NA;     // BIDS-friendly
    public MissingPolicy csvMissing = MissingPolicy.Blank;  // Excel/Sheets-friendly
    string Miss(MissingPolicy p) => p == MissingPolicy.Blank ? "" : (p == MissingPolicy.NA ? "n/a" : "NaN");

    /* -------------------- Random -------------------- */
    [Header("Rand")]
    public int randomSeed = 1234;

    /* -------------------- Datos -------------------- */
    [Serializable] public class Trial
    {
        public int trial_id;
        public int block_or_run;
        public string trial_type;      // "go" | "stop"
        public int cue_id;             // 1 = Certain (green), 2 = Uncertain (white en paper; aquí rojo tenue)
        public long stim_onset_ms;
        public long stop_onset_ms;     // beep/rojo onset relativo a t0; -1 en Go
        public int ssd_ms;             // solo stop
        public int trial_duration_ms;
        public int iti_ms;

        // Estado al onset
        public bool moving_at_onset;
        public bool key_held_onset;

        // RTs
        public bool key_down;
        public int keydown_rt_ms;      // desde onset a primer KeyDown
        public bool moved_on_go;
        public int rt_go_ms;           // cuando supera speed threshold (si estaba quieto)
        public int key_release_rt_ms;  // desde beep a soltar
        public int rt_stop_ms;         // igual que key_release_rt_ms pero validado

        // Outcome
        public bool anticipation;
        public bool go_omission;
        public bool stop_success;
        public bool stop_commission;
        public int correctness;        // 1 correcto, 0 incorrecto
        public string resp_key;        // "space" o "none"
        public float pre_beep_speed;
    }

    [Serializable] public class Summary
    {
        public string session_id;
        public string started_at_utc;
        public string ended_at_utc;
        public int blocks_or_runs;
        public int trials_per_block;
        public int n_trials;
        public int go_trials;
        public int stop_trials;
        public float p_stop;

        public float stop_success_rate;
        public int rt_go_median_ms;
        public float rt_go_cv;
        public int ssd_mean_ms;
        public int ssrt_ms;

        public int ssrt_integration_ms;
        public int rt_go_q_ms;

        public int go_omissions;
        public int stop_commissions;
        public int anticipations;
    }

    [Serializable] public class Session
    {
        public Summary summary = new Summary();
        public List<Trial> trials = new List<Trial>();
    }

    Session _session = new Session();
    Trial _lastTrial = null;
    System.Random _rng;
    long _t0ms;
    int _trialCounter = 0;
    int _currentSSD;

    List<int> _ssdList = new List<int>();
    List<int> _rtGo = new List<int>();        // RT válidos (keydown o velocidad; usamos velocidad si estava quieto)
    List<int> _goRTSerial = new List<int>();  // >=0 válido, -1 omisión, -2 anticipación

    int _nStop = 0, _nStopSucc = 0;
    int _goOmissions = 0;
    int _stopCommissions = 0;
    int _anticipations = 0;

    void Start()
    {
        _rng = new System.Random(randomSeed);
        _currentSSD = ssdStartMs;

        _session.summary.session_id = "SST_RUN_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        _session.summary.started_at_utc = DateTime.UtcNow.ToString("o");
        _session.summary.blocks_or_runs = csstMode ? runs : blocks;
        _session.summary.trials_per_block = csstMode ? (perRunCertainGo + perRunUncertainGo + perRunStop) : trialsPerBlock;
        _session.summary.p_stop = csstMode
            ? (float)perRunStop / (_session.summary.trials_per_block)
            : stopProportion;

        if (lightCue) lightCue.ShowGreen(0f);
        if (luzVerde) luzVerde.SetActive(true);
        if (luzRoja)  luzRoja.SetActive(false);

        if (timerHUD == null) timerHUD = FindObjectOfType<SSTTimerHUD>();
        if (timerHUD){ timerHUD.manager = this; timerHUD.mode = SSTTimerHUD.Mode.Countdown; timerHUD.autoComputeFromManager = true; timerHUD.ComputeFromManager(); }

        StartCoroutine(BootstrapAndRun());
    }

    IEnumerator BootstrapAndRun()
    {
        if (!startUI)
        {
            startUI = FindObjectOfType<StartUIPanel>();
            if (!startUI){ var go = new GameObject("StartUIPanel(Auto)"); startUI = go.AddComponent<StartUIPanel>(); }
        }

        bool proceed = false;
        string title = csstMode ? "Semáforo (CSST)" : "Semáforo";
        string body  = csstMode
            ? "CUE 200ms: Verde = casi seguro avanzar (no habrá Alto). Rojo tenue = puede haber Alto.\nGO: Mantén ESPACIO para avanzar.\nSTOP: Suelta ESPACIO pronto y mantén suelto."
            : "Cuando esté VERDE, mantén ESPACIO para avanzar. A veces escucharás un BEEP: ¡ALTO! Debes SOLTAR ESPACIO lo más rápido posible y quedarte quieto.";
        startUI.Show(title, body, ()=>proceed=true);
        yield return new WaitUntil(()=>proceed);

        yield return CountdownOverlay.ShowAndWait(countdownSeconds, "¡Prepárate!", tickSfx, finalSfx);

        if (timerHUD) timerHUD.StartTimer();
        _t0ms = NowMs();

        if (csstMode) yield return RunExperimentCSST();
        else          yield return RunExperimentSimple();

        if (timerHUD) timerHUD.StopTimer();
        _session.summary.ended_at_utc = DateTime.UtcNow.ToString("o");

        FinalizeMetrics();
        SaveJson();
        SaveEventsTsv();
        SaveEventsCsv();
    }

    /* -------------------- Experimentos -------------------- */

    IEnumerator RunExperimentSimple()
    {
        for (int b = 1; b <= blocks; b++)
        {
            int nStop = Mathf.RoundToInt(trialsPerBlock * stopProportion);
            int nGo   = trialsPerBlock - nStop;
            var bag = new List<(int cue_id,string type)>(); // cue_id=0 (no usa)
            for (int i=0;i<nGo;i++) bag.Add((0,"go"));
            for (int i=0;i<nStop;i++) bag.Add((0,"stop"));
            Shuffle(bag);

            for (int t = 0; t < trialsPerBlock; t++)
            {
                yield return RunTrial(b, bag[t].type, bag[t].cue_id);
                float _iti = RandomRange(itiMin, itiMax);
                if (_lastTrial != null) _lastTrial.iti_ms = Mathf.RoundToInt(_iti * 1000f);
                yield return new WaitForSeconds(_iti);
            }
        }
    }

    IEnumerator RunExperimentCSST()
    {
        // Construcción por run: 32 CG, 32 UG, 16 Stop (UG)
        int perRunTotal = perRunCertainGo + perRunUncertainGo + perRunStop;
        for (int r = 1; r <= runs; r++)
        {
            var trials = new List<(int cue_id,string type)>();
            for (int i=0;i<perRunCertainGo;i++)   trials.Add((1,"go"));   // Certain-Go
            for (int i=0;i<perRunUncertainGo;i++) trials.Add((2,"go"));   // Uncertain-Go
            for (int i=0;i<perRunStop;i++)        trials.Add((2,"stop")); // Uncertain-Stop
            Shuffle(trials);

            for (int t = 0; t < perRunTotal; t++)
            {
                yield return RunTrial(r, trials[t].type, trials[t].cue_id);
                float _iti = RandomRange(itiMin, itiMax); // en CSST sugerido 1-4 s
                if (_lastTrial != null) _lastTrial.iti_ms = Mathf.RoundToInt(_iti * 1000f);
                yield return new WaitForSeconds(_iti);
            }
        }
    }

    IEnumerator RunTrial(int blockOrRun, string type, int cue_id)
    {
        _trialCounter++;
        long onset = NowMs();

        // ---- CUE (solo CSST) ----
        if (csstMode)
        {
            // Cue: 1 = Certain (verde), 2 = Uncertain (rojo tenue para diferenciar)
            if (lightCue)
            {
                if (cue_id == 1) lightCue.ShowGreen(0f);
                else             lightCue.ShowRedInstant(); // usamos rojo como "uncertain cue"
            }
            if (luzVerde) luzVerde.SetActive(cue_id==1);
            if (luzRoja)  luzRoja.SetActive(cue_id==2);
            yield return new WaitForSeconds(cueDurationMs/1000f);
        }

        // ---- GO onset ----
        var tr = new Trial
        {
            trial_id = _trialCounter,
            block_or_run = blockOrRun,
            trial_type = type,
            cue_id = csstMode ? cue_id : 0,
            stim_onset_ms = (long)(NowMs() - _t0ms),
            stop_onset_ms = -1,
            ssd_ms = type == "stop" ? _currentSSD : 0,
            trial_duration_ms = stimDurationMs,
            iti_ms = -1,

            moving_at_onset = runner.CurrentSpeed() > moveSpeedThreshold,
            key_held_onset  = Input.GetKey(runner.moveKey),

            key_down = false,
            keydown_rt_ms = -1,
            moved_on_go = false,
            rt_go_ms = -1,
            key_release_rt_ms = -1,
            rt_stop_ms = -1,

            anticipation = false,
            go_omission = false,
            stop_success = false,
            stop_commission = false,
            correctness = 0,
            resp_key = "none",
            pre_beep_speed = -1f
        };

        // Visual GO: siempre verde
        if (lightCue) lightCue.ShowGreen(0f);
        if (luzVerde) luzVerde.SetActive(true);
        if (luzRoja)  luzRoja.SetActive(false);

        bool isStop = (type == "stop");
        bool beepHecho = false;
        long beepTime = NowMs() + tr.ssd_ms;
        tr.stop_onset_ms = isStop ? (long)(beepTime - _t0ms) : -1;

        bool goRTtaken = false;
        bool stopSuccessEvaluated = false;
        long stopSuccessDeadline = 0;
        long releaseDetectedAt = -1;
        long releaseHoldUntil = -1;

        while (NowMs() - onset < stimDurationMs)
        {
            long now = NowMs();

            // Primer KeyDown explícito
            if (!tr.key_down && Input.GetKeyDown(runner.moveKey))
            {
                tr.key_down = true;
                tr.keydown_rt_ms = (int)(now - onset);
                tr.resp_key = "space";
            }

            // RT-Go (si estaba quieto al inicio)
            if (!goRTtaken && !tr.moving_at_onset && runner.CurrentSpeed() > moveSpeedThreshold)
            {
                int rt = (int)(now - onset);
                if (rt < rtMinMs)
                {
                    tr.anticipation = true;
                    _anticipations++;
                    goRTtaken = true;
                }
                else
                {
                    tr.moved_on_go = true;
                    tr.rt_go_ms = rt;
                    _rtGo.Add(rt);
                    goRTtaken = true;
                }
            }

            // Lanzar STOP (beep + rojo)
            if (isStop && !beepHecho && now >= beepTime)
            {
                tr.pre_beep_speed = runner.CurrentSpeed();
                beepHecho = true;
                tr.stop_onset_ms = (long)(now - _t0ms);
                tr.ssd_ms = (int)(now - onset);

                if (stopBeep) stopBeep.Play();
                if (lightCue) lightCue.ShowRedInstant();
                if (luzRoja)  luzRoja.SetActive(true);
                if (luzVerde) luzVerde.SetActive(false);

                stopSuccessDeadline = now + stopSuccessWindowMs;
            }

            // Registrar release
            if (isStop && beepHecho && tr.key_release_rt_ms < 0 && !Input.GetKey(runner.moveKey))
            {
                tr.key_release_rt_ms = (int)(now - beepTime);
                releaseDetectedAt = now;
                releaseHoldUntil = now + stopHoldMs;
            }

            // Éxito del Stop
            if (isStop && beepHecho && !stopSuccessEvaluated)
            {
                bool releasedInWindow = (releaseDetectedAt > 0) && (releaseDetectedAt <= stopSuccessDeadline);
                bool heldEnough = releasedInWindow && (now >= releaseHoldUntil) && !Input.GetKey(runner.moveKey);
                bool speedOk = !requireSpeedBelowOnStop || (runner.CurrentSpeed() <= moveSpeedThreshold);

                if (heldEnough && speedOk)
                {
                    tr.stop_success = true;
                    tr.rt_stop_ms = (int)(releaseDetectedAt - beepTime);
                    stopSuccessEvaluated = true;
                }
                else if (now >= stopSuccessDeadline)
                {
                    tr.stop_success = false;
                    tr.rt_stop_ms = -1;
                    stopSuccessEvaluated = true;
                }
            }

            yield return null;
        }

        // Fin trial → baseline verde
        if (lightCue) lightCue.ShowGreen(0f);
        if (luzVerde) luzVerde.SetActive(true);
        if (luzRoja)  luzRoja.SetActive(false);

        // Outcomes GO / STOP
        if (tr.trial_type == "go")
        {
            bool responded = tr.moved_on_go || tr.key_down || tr.key_held_onset || tr.moving_at_onset;
            if ((tr.key_held_onset || tr.moving_at_onset) && tr.rt_go_ms < 0 && !tr.anticipation)
                tr.rt_go_ms = 0;

            tr.go_omission = !responded && !tr.anticipation;
            if (tr.go_omission) _goOmissions++;

            if (!tr.go_omission && !tr.anticipation && tr.rt_go_ms >= 0)
                _rtGo.Add(tr.rt_go_ms);

            if (tr.go_omission) _goRTSerial.Add(-1);
            else if (tr.anticipation) _goRTSerial.Add(-2);
            else _goRTSerial.Add(Mathf.Max(0, tr.rt_go_ms));
        }
        else
        {
            tr.stop_commission = !tr.stop_success;
            if (tr.stop_commission) _stopCommissions++;
        }

        // Correctness
        tr.correctness = (tr.trial_type == "go")
            ? ((!tr.go_omission && !tr.anticipation) ? 1 : 0)
            : (tr.stop_success ? 1 : 0);

        // Staircase
        if (tr.trial_type == "stop")
        {
            _nStop++;
            if (tr.stop_success){ _nStopSucc++; _currentSSD = Mathf.Min(_currentSSD + ssdStepMs, ssdMaxMs); }
            else                { _currentSSD = Mathf.Max(_currentSSD - ssdStepMs, ssdMinMs); }
            _ssdList.Add(tr.ssd_ms);
        }

        _session.trials.Add(tr);
        _lastTrial = tr;
    }

    /* -------------------- Métricas y Export -------------------- */

    void FinalizeMetrics()
    {
        _session.summary.n_trials = _session.trials.Count;
        _session.summary.stop_trials = _nStop;
        _session.summary.go_trials = _session.trials.Count - _nStop;
        _session.summary.stop_success_rate = _nStop > 0 ? (float)_nStopSucc / _nStop : 0f;

        int rtMed = _rtGo.Count > 0 ? Median(_rtGo) : -1;
        float rtCV = CV(_rtGo);
        int ssdMean = _ssdList.Count > 0 ? Mathf.RoundToInt((float)_ssdList.Average()) : -1;
        int ssrt = (rtMed >= 0 && ssdMean >= 0) ? Mathf.Max(0, rtMed - ssdMean) : -1;

        _session.summary.rt_go_median_ms = rtMed;
        _session.summary.rt_go_cv = rtCV;
        _session.summary.ssd_mean_ms = ssdMean;
        _session.summary.ssrt_ms = ssrt;

        float pRespond = 1f - _session.summary.stop_success_rate;
        int qRT = Quantile(_rtGo, pRespond);
        int ssrtInt = (qRT >= 0 && ssdMean >= 0) ? Mathf.Max(0, qRT - ssdMean) : -1;
        _session.summary.rt_go_q_ms = qRT;
        _session.summary.ssrt_integration_ms = ssrtInt;

        _session.summary.go_omissions     = _goOmissions;
        _session.summary.stop_commissions = _stopCommissions;
        _session.summary.anticipations    = _anticipations;
    }

    void SaveJson()
    {
        string json = JsonUtility.ToJson(_session, true);
        string path = System.IO.Path.Combine(Application.persistentDataPath, _session.summary.session_id + ".json");
        System.IO.File.WriteAllText(path, json);
        Debug.Log("[SST-CSST] Guardado JSON: " + path);
    }

    void SaveEventsTsv()
    {
        string m = Miss(tsvMissing);
        try
        {
            var lines = new List<string>();
            // cue_onset = onset del STOP (para compatibilidad con tu pipeline actual)
            lines.Add("onset\tduration\ttrialid\tcue_onset\tgng_id\tcue_id\tcorr_resp\tresponse\tcorrectness\trt");
            foreach (var tr in _session.trials)
            {
                float onset_s = tr.stim_onset_ms / 1000f;
                float dur_s   = tr.trial_duration_ms / 1000f;
                string cueOn  = tr.stop_onset_ms >= 0 ? (tr.stop_onset_ms / 1000f).ToString("0.###") : m;
                int gng       = tr.trial_type == "go" ? 1 : 2;
                int corrResp  = 1; // en este paradigma: mantener ESPACIO

                int resp_go   = ((tr.trial_type=="go")   && (!tr.go_omission && !tr.anticipation)) ? 1 : 0;
                int resp_stop = ((tr.trial_type=="stop") && tr.stop_commission) ? 1 : 0;
                int resp      = (tr.trial_type=="go") ? resp_go : resp_stop;

                string rtStr;
                if (tr.trial_type == "go")
                    rtStr = (tr.rt_go_ms >= 0) ? (tr.rt_go_ms/1000f).ToString("0.###") : m;
                else
                    rtStr = (tr.key_release_rt_ms >= 0) ? (tr.key_release_rt_ms/1000f).ToString("0.###") : m;

                lines.Add(string.Join("\t", new [] {
                    onset_s.ToString("0.###"),
                    dur_s.ToString("0.###"),
                    tr.trial_id.ToString(),
                    cueOn,
                    gng.ToString(),
                    (csstMode ? tr.cue_id.ToString() : m),
                    corrResp.ToString(),
                    resp.ToString(),
                    tr.correctness.ToString(),
                    rtStr
                }));
            }
            string path = System.IO.Path.Combine(Application.persistentDataPath, _session.summary.session_id + "_events.tsv");
            System.IO.File.WriteAllLines(path, lines);
            Debug.Log("[SST-CSST] TSV guardado: " + path);
        }
        catch (Exception e){ Debug.unityLogger.LogWarning("SST-CSST", "Error TSV: " + e.Message); }
    }

    void SaveEventsCsv()
    {
        string m = Miss(csvMissing);
        try
        {
            var lines = new List<string>();
            lines.Add("onset,duration,trialid,cue_onset,gng_id,cue_id,corr_resp,response,correctness,rt");
            foreach (var tr in _session.trials)
            {
                string onset_s = (tr.stim_onset_ms / 1000f).ToString("0.###");
                string dur_s   = (tr.trial_duration_ms / 1000f).ToString("0.###");
                string cueOn   = tr.stop_onset_ms >= 0 ? (tr.stop_onset_ms / 1000f).ToString("0.###") : m;
                int gng        = tr.trial_type == "go" ? 1 : 2;
                int corrResp   = 1;

                int resp_go   = ((tr.trial_type=="go")   && (!tr.go_omission && !tr.anticipation)) ? 1 : 0;
                int resp_stop = ((tr.trial_type=="stop") && tr.stop_commission) ? 1 : 0;
                int resp      = (tr.trial_type=="go") ? resp_go : resp_stop;

                string rtStr;
                if (tr.trial_type == "go")
                    rtStr = (tr.rt_go_ms >= 0) ? (tr.rt_go_ms/1000f).ToString("0.###") : m;
                else
                    rtStr = (tr.key_release_rt_ms >= 0) ? (tr.key_release_rt_ms/1000f).ToString("0.###") : m;

                lines.Add(string.Join(",", new [] {
                    onset_s, dur_s, tr.trial_id.ToString(), cueOn, gng.ToString(),
                    (csstMode ? tr.cue_id.ToString() : m),
                    corrResp.ToString(), resp.ToString(), tr.correctness.ToString(), rtStr
                }));
            }
            string path = System.IO.Path.Combine(Application.persistentDataPath, _session.summary.session_id + "_events.csv");
            System.IO.File.WriteAllLines(path, lines);
            Debug.Log("[SST-CSST] CSV guardado: " + path);
        }
        catch (Exception e){ Debug.unityLogger.LogWarning("SST-CSST", "Error CSV: " + e.Message); }
    }

    /* -------------------- Helpers -------------------- */

    long NowMs() => (long)(Time.realtimeSinceStartup * 1000f);

    void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int k = _rng.Next(i + 1);
            var tmp = list[i];
            list[i] = list[k];
            list[k] = tmp;
        }
    }

    float RandomRange(float a, float b) => (float)(_rng.NextDouble() * (b - a) + a);

    int Quantile(List<int> xs, float p)
    {
        if (xs == null || xs.Count == 0) return -1;
        var o = xs.OrderBy(v => v).ToList();
        p = Mathf.Clamp01(p);
        int idx = Mathf.CeilToInt(p * o.Count) - 1;
        if (idx < 0) idx = 0;
        if (idx >= o.Count) idx = o.Count - 1;
        return o[idx];
    }

    int Median(List<int> xs)
    {
        if (xs == null || xs.Count == 0) return -1;
        var o = xs.OrderBy(v => v).ToList();
        int m = o.Count / 2;
        return (o.Count % 2 == 1) ? o[m] : Mathf.RoundToInt((o[m - 1] + o[m]) / 2f);
    }

    float CV(List<int> xs)
    {
        if (xs == null || xs.Count < 2) return -1f;
        float mean = (float)xs.Average();
        float v = 0f;
        foreach (var x in xs){ float d = x - mean; v += d*d; }
        v /= (xs.Count - 1);
        float sd = Mathf.Sqrt(v);
        return mean > 0 ? sd / mean : -1f;
    }
}
