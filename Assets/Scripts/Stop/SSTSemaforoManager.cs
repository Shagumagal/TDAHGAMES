using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class SSTSemaforoManager : MonoBehaviour
{
    /* -------------------- UI -------------------- */
    [Header("UI")]
    public SSTTimerHUD timerHUD;  // arrástralo en el inspector (o se busca solo)

    public StartUIPanel startUI;         // (opcional) arrástralo en el inspector
    public int countdownSeconds = 3;
    public AudioClip tickSfx;            // (opcional) beep por segundo
    public AudioClip finalSfx;           // (opcional) beep final

    /* -------------------- Refs juego -------------------- */
    [Header("Refs")]
    public SSTRunner runner;             // “Jugador” que avanza (usa Rigidbody y CurrentSpeed())
    public AudioSource stopBeep;         // Sonido del Stop
    public SSTLightCue lightCue;         // Overlay UI: verde/rojo (CanvasGroup)

    // (Opcional) si quieres mantener objetos físicos de luz en escena:
    public GameObject luzVerde;          // Farol/mesh verde (opcional)
    public GameObject luzRoja;           // Farol/mesh rojo  (opcional)

    /* -------------------- Diseño -------------------- */
    [Header("Diseño")]
    public int blocks = 2;
    public int trialsPerBlock = 24;
    [Range(0f,1f)] public float stopProportion = 0.25f;

    /* -------------------- Timing -------------------- */
    [Header("Timing")]
    public int stimDurationMs = 3000;    // Duración de cada trial
    public float itiMin = 1.0f;
    public float itiMax = 1.5f;

    /* -------------------- Staircase SSD -------------------- */
    [Header("SSD Staircase")]
    public int ssdStartMs = 250;
    public int ssdStepMs = 50;
    public int ssdMinMs = 50;
    public int ssdMaxMs = 700;

    /* -------------------- Criterios respuesta -------------------- */
    [Header("Criterios de respuesta")]
    public float moveSpeedThreshold = 0.10f; // Umbral de “se mueve”
    public int rtMinMs = 150;                // Anticipaciones por debajo de esto no valen
    public int stopSuccessWindowMs = 800;    // Ventana para frenar tras beep

    /* -------------------- Random -------------------- */
    [Header("Rand")]
    public int randomSeed = 1234;

    /* -------------------- Tipos de datos -------------------- */
    [Serializable] public class VigilanceQuartile
    {
        public int q_index;             // 1..4
        public int n_go;
        public int omissions;
        public int anticipations;
        public int rt_median_ms;        // solo válidos (>= rtMinMs)
        public float omission_rate;     // omissions / n_go
        public float anticipation_rate; // anticipations / n_go
    }

    [Serializable] public class Trial
    {
        public bool moving_at_onset;    // ¿ya se movía al empezar el verde?
        public int trial_id;
        public int block_index;
        public string trial_type;       // "go" | "stop"
        public long stim_onset_ms;
        public int ssd_ms;              // solo en stop
        public bool moved_on_go;        // se movió en Go (si estaba quieto al inicio)
        public int rt_go_ms;            // tiempo a empezar a moverse (>threshold)
        public bool stop_beeped;
        public bool stop_success;       // se detuvo en ventana
        public int rt_stop_ms;          // tiempo en frenar bajo threshold

        // --- Nuevos flags para métricas DSM-friendly ---
        public bool anticipation;       // true si hubo intento con rt < rtMinMs en Go
        public bool go_omission;        // true si NO se movió en Go en la ventana del trial
        public bool stop_commission;    // true si falló el Stop (complemento de stop_success)
    }

    [Serializable] public class Summary
    {
        public string session_id;
        public string started_at_utc;
        public string ended_at_utc;
        public int blocks;
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

        // --- Nuevas métricas ---
        public int go_omissions;            // Go sin respuesta válida
        public int stop_commissions;        // Stop fallidos
        public int anticipations;           // RT < rtMinMs en Go
        public List<VigilanceQuartile> vigilance = new List<VigilanceQuartile>();
    }

    [Serializable] public class Session
    {
        public Summary summary = new Summary();
        public List<Trial> trials = new List<Trial>();
    }

    /* -------------------- Estado interno -------------------- */
    Session _session = new Session();
    System.Random _rng;
    long _t0ms;
    int _trialCounter = 0;
    int _currentSSD;

    List<int> _ssdList = new List<int>();
    List<int> _rtGo = new List<int>();         // RT válidos (>= rtMinMs) para estadísticos globales
    List<int> _goRTSerial = new List<int>();   // Serie temporal por trial Go: >=0 válido, -1 omisión, -2 anticipación

    int _nStop = 0, _nStopSucc = 0;
    int _goOmissions = 0;
    int _stopCommissions = 0;
    int _anticipations = 0;

    /* -------------------- Ciclo de vida -------------------- */
    void Start()
    {
        _rng = new System.Random(randomSeed);
        _currentSSD = ssdStartMs;

        _session.summary.session_id = "SST_RUN_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        _session.summary.started_at_utc = DateTime.UtcNow.ToString("o");
        _session.summary.blocks = blocks;
        _session.summary.trials_per_block = trialsPerBlock;
        _session.summary.p_stop = stopProportion;

        // Baseline: VERDE encendido desde el inicio
        if (lightCue) lightCue.ShowGreen(0f);
        if (luzVerde) luzVerde.SetActive(true);
        if (luzRoja)  luzRoja.SetActive(false);
        if (timerHUD == null) timerHUD = FindObjectOfType<SSTTimerHUD>();
    if (timerHUD)
    {
    timerHUD.manager = this;
    timerHUD.mode = SSTTimerHUD.Mode.Countdown;
    timerHUD.autoComputeFromManager = true;
    timerHUD.ComputeFromManager(); // calcula duración estimada
    }

        StartCoroutine(BootstrapAndRun());
    }

    IEnumerator BootstrapAndRun()
    {
        // 1) Garantiza StartUIPanel
        if (!startUI)
        {
            if (!StartUIPanel.Instance)
            {
                var go = new GameObject("StartUIPanel_Runtime");
                startUI = go.AddComponent<StartUIPanel>();
                startUI.BuildInScene(); // crea Canvas + card + botón
            }
            else startUI = StartUIPanel.Instance;
        }

        // 2) Instrucciones
        bool proceed = false;
        startUI.Show(
            "Semáforo — Reglas",
            "Mantén <b>AVANZAR</b> mientras la luz está <b>VERDE</b>.\n" +
            "Si suena el <b>beep</b> y cambia a <b>ROJO</b>, <b>DETENTE</b> lo más rápido posible.",
            () => proceed = true
        );
        yield return new WaitUntil(() => proceed);

        // 3) Cuenta regresiva
        yield return CountdownOverlay.ShowAndWait(countdownSeconds, "¡Prepárate!", tickSfx, finalSfx);

        // 4) Experimento
        if (timerHUD) timerHUD.StartTimer();
        _t0ms = NowMs();
        yield return RunExperiment();
        if (timerHUD) timerHUD.StopTimer();

        // 5) (Opcional) Mensaje final
        // startUI.Show("¡Listo!", "Has terminado este ejercicio.", null);
    }

    IEnumerator RunExperiment()
    {
        _t0ms = NowMs();

        for (int b = 1; b <= blocks; b++)
        {
            int nStop = Mathf.RoundToInt(trialsPerBlock * stopProportion);
            int nGo   = trialsPerBlock - nStop;
            List<string> bag = new List<string>();
            for (int i = 0; i < nGo;   i++) bag.Add("go");
            for (int i = 0; i < nStop; i++) bag.Add("stop");
            Shuffle(bag);

            // (Opcional) mini countdown por bloque:
            // yield return CountdownOverlay.ShowAndWait(2, $"Bloque {b}/{blocks}", tickSfx, null);

            for (int t = 0; t < trialsPerBlock; t++)
            {
                yield return RunTrial(b, bag[t]);
                yield return new WaitForSeconds(RandomRange(itiMin, itiMax));
            }
        }

        _session.summary.ended_at_utc = DateTime.UtcNow.ToString("o");
        FinalizeMetrics();
        SaveJson();

        Debug.Log($"[SST-Semáforo] FIN. SSRT={_session.summary.ssrt_ms} ms  " +
                  $"StopSucc={_session.summary.stop_success_rate:P1}  " +
                  $"GoOmissions={_session.summary.go_omissions}  " +
                  $"StopCommissions={_session.summary.stop_commissions}  " +
                  $"Anticipations={_session.summary.anticipations}");
    }

    IEnumerator RunTrial(int blockIndex, string trialType)
    {
        _trialCounter++;
        var tr = new Trial
        {
            moving_at_onset = false,
            trial_id = _trialCounter,
            block_index = blockIndex,
            trial_type = trialType,
            ssd_ms = trialType == "stop" ? _currentSSD : 0,
            moved_on_go = false,
            rt_go_ms = -1,
            stop_beeped = false,
            stop_success = false,
            rt_stop_ms = -1
        };

        // *** NO limpiar aquí: mantenemos baseline VERDE ***
        // (Nada de Clear() ni apagar luces al inicio del trial)

        // ONSET GO
        long onset = NowMs();
        tr.stim_onset_ms = (long)(onset - _t0ms);

        // Refrescar VERDE (opcional con un leve fade, mantiene baseline)
        if (lightCue) lightCue.ShowGreen(200f);
        if (luzVerde) luzVerde.SetActive(true);
        if (luzRoja)  luzRoja.SetActive(false);

        // Estado al inicio del verde
        tr.moving_at_onset = runner.CurrentSpeed() > moveSpeedThreshold;

        bool isStop = (trialType == "stop");
        bool beepHecho = false;
        long beepTime = onset + tr.ssd_ms;

        bool goRTtaken = false;
        bool stopSuccessEvaluated = false;
        long stopSuccessDeadline = 0;

        while (NowMs() - onset < stimDurationMs)
        {
            long now = NowMs();

            // RT-Go (solo si estaba quieto al inicio)
            if (!goRTtaken && !tr.moving_at_onset && runner.CurrentSpeed() > moveSpeedThreshold)
            {
                int rt = (int)(now - onset);

                if (rt < rtMinMs)
                {
                    // ANTICIPACIÓN
                    tr.anticipation = true;
                    _anticipations++;
                    goRTtaken = true; // se cuenta como intento (anticipado)
                }
                else
                {
                    // RESPUESTA VÁLIDA EN GO
                    tr.moved_on_go = true;
                    tr.rt_go_ms = rt;
                    _rtGo.Add(rt);
                    goRTtaken = true;
                }
            }

            // STOP (beep + rojo)
            if (isStop && !beepHecho && now >= beepTime)
            {
                beepHecho = true;
                tr.stop_beeped = true;
                if (stopBeep) stopBeep.Play();

                // Rojo instantáneo para onset nítido
                if (lightCue) lightCue.ShowRedInstant();
                if (luzRoja)  luzRoja.SetActive(true);
                if (luzVerde) luzVerde.SetActive(false);

                stopSuccessDeadline = now + stopSuccessWindowMs;
            }

            // Éxito de Stop = bajar bajo umbral antes del deadline
            if (isStop && beepHecho && !stopSuccessEvaluated)
            {
                if (runner.CurrentSpeed() <= moveSpeedThreshold)
                {
                    tr.stop_success = true;
                    tr.rt_stop_ms = (int)(now - (onset + tr.ssd_ms));
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

        // Fin trial → volver a VERDE (baseline para el siguiente)
        if (lightCue) lightCue.ShowGreen(0f);
        if (luzVerde) luzVerde.SetActive(true);
        if (luzRoja)  luzRoja.SetActive(false);

        // Derivar flags finales por tipo
        if (tr.trial_type == "go")
        {
            // Omisión = nunca se movió válidamente y tampoco anticipó
            tr.go_omission = !tr.moved_on_go && !tr.anticipation;
            if (tr.go_omission) _goOmissions++;

            // Serie temporal para vigilancia (uno por cada trial Go en orden)
            if (tr.moved_on_go && tr.rt_go_ms >= 0) _goRTSerial.Add(tr.rt_go_ms);
            else if (tr.anticipation)                _goRTSerial.Add(-2);
            else                                     _goRTSerial.Add(-1);
        }
        else // stop
        {
            tr.stop_commission = !tr.stop_success;
            if (tr.stop_commission) _stopCommissions++;
        }

        // Staircase y contadores (solo Stop)
        if (isStop)
        {
            _nStop++;
            if (tr.stop_success)
            {
                _nStopSucc++;
                _currentSSD = Mathf.Min(_currentSSD + ssdStepMs, ssdMaxMs);
            }
            else
            {
                _currentSSD = Mathf.Max(_currentSSD - ssdStepMs, ssdMinMs);
            }
            _ssdList.Add(tr.ssd_ms);
        }

        _session.trials.Add(tr);
    }

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

        // Nuevos contadores globales
        _session.summary.go_omissions     = _goOmissions;
        _session.summary.stop_commissions = _stopCommissions;
        _session.summary.anticipations    = _anticipations;

        // Vigilance por cuartiles (Q1..Q4)
        ComputeVigilanceQuartiles();
    }

    void ComputeVigilanceQuartiles()
    {
        var series = _goRTSerial; // orden temporal de trials Go
        int n = series.Count;
        if (n == 0) return;

        int baseSize = n / 4;
        int remainder = n % 4;
        int start = 0;

        for (int q = 1; q <= 4; q++)
        {
            int size = baseSize + (q <= remainder ? 1 : 0);
            if (size == 0) continue;

            var slice = series.GetRange(start, size);
            start += size;

            int n_go = slice.Count;
            int omissions = slice.Count(x => x == -1);
            int anticips  = slice.Count(x => x == -2);

            var validRTs = slice.Where(x => x >= 0).ToList();
            int medRT = validRTs.Count > 0 ? Median(validRTs) : -1;

            var vq = new VigilanceQuartile
            {
                q_index = q,
                n_go = n_go,
                omissions = omissions,
                anticipations = anticips,
                rt_median_ms = medRT,
                omission_rate = n_go > 0 ? (float)omissions / n_go : 0f,
                anticipation_rate = n_go > 0 ? (float)anticips / n_go : 0f
            };

            _session.summary.vigilance.Add(vq);
        }
    }

    void SaveJson()
    {
        string json = JsonUtility.ToJson(_session, true);
        string path = System.IO.Path.Combine(Application.persistentDataPath, _session.summary.session_id + ".json");
        System.IO.File.WriteAllText(path, json);
        Debug.Log("[SST-Semáforo] Guardado: " + path);
    }

    /* -------------------- Helpers -------------------- */
    long NowMs() => (long)(Time.realtimeSinceStartup * 1000f);

    void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int k = _rng.Next(i + 1);
            (list[i], list[k]) = (list[k], list[i]);
        }
    }

    float RandomRange(float a, float b)
    {
        return (float)(_rng.NextDouble() * (b - a) + a);
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
        foreach (var x in xs) { float d = x - mean; v += d * d; }
        v /= (xs.Count - 1);
        float sd = Mathf.Sqrt(v);
        return mean > 0 ? sd / mean : -1f;
    }
}
