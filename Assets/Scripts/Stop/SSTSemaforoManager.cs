using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class SSTSemaforoManager : MonoBehaviour
{
    [Header("Refs")]
    public SSTRunner runner;           // El “jugador” que avanza con Space
    public GameObject luzVerde;        // Muestra “Go”
    public GameObject luzRoja;         // Muestra “Stop”
    public AudioSource stopBeep;       // Sonido del Stop

    [Header("Diseño")]
    public int blocks = 2;
    public int trialsPerBlock = 24;
    [Range(0f,1f)] public float stopProportion = 0.25f;

    [Header("Timing")]
    public int stimDurationMs = 3000;  // Duración del “semáforo” por trial
    public float itiMin = 1.0f;
    public float itiMax = 1.5f;

    [Header("SSD Staircase")]
    public int ssdStartMs = 250;
    public int ssdStepMs = 50;
    public int ssdMinMs = 50;
    public int ssdMaxMs = 700;

    [Header("Criterios de respuesta")]
    public float moveSpeedThreshold = 0.10f; // Velocidad para contar “se mueve”
    public int rtMinMs = 150;               // Anticipaciones por debajo de esto no cuentan
    public int stopSuccessWindowMs = 800;   // Ventana para frenar tras beep

    [Header("Rand")]
    public int randomSeed = 1234;

    [Serializable] public class Trial
    {
        public int trial_id;
        public int block_index;
        public string trial_type; // "go" | "stop"
        public long stim_onset_ms;
        public int ssd_ms; // solo en stop
        public bool moved_on_go; // se movió en Go
        public int rt_go_ms;     // tiempo a empezar a moverse (>threshold)
        public bool stop_beeped;
        public bool stop_success;    // se detuvo en ventana
        public int rt_stop_ms;       // tiempo en frenar bajo threshold
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
    }

    [Serializable] public class Session
    {
        public Summary summary = new Summary();
        public List<Trial> trials = new List<Trial>();
    }

    Session _session = new Session();
    System.Random _rng;
    long _t0ms;
    int _trialCounter = 0;
    int _currentSSD;
    List<int> _ssdList = new List<int>();
    List<int> _rtGo = new List<int>();
    int _nStop=0, _nStopSucc=0, _nGo=0;

    void Start()
    {
        _rng = new System.Random(randomSeed);
        _currentSSD = ssdStartMs;

        _session.summary.session_id = "SST_RUN_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        _session.summary.started_at_utc = DateTime.UtcNow.ToString("o");
        _session.summary.blocks = blocks;
        _session.summary.trials_per_block = trialsPerBlock;
        _session.summary.p_stop = stopProportion;

        runner.allowControl = true; // puede presionar Space para moverse
        if (luzVerde) luzVerde.SetActive(false);
        if (luzRoja)  luzRoja.SetActive(false);

        StartCoroutine(RunExperiment());
    }

    IEnumerator RunExperiment()
    {
        _t0ms = NowMs();

        for (int b = 1; b <= blocks; b++)
        {
            // Bolsa de ensayos
            int nStop = Mathf.RoundToInt(trialsPerBlock * stopProportion);
            int nGo   = trialsPerBlock - nStop;
            List<string> bag = new List<string>();
            for (int i=0;i<nGo;i++) bag.Add("go");
            for (int i=0;i<nStop;i++) bag.Add("stop");
            Shuffle(bag);

            for (int t = 0; t < trialsPerBlock; t++)
            {
                yield return RunTrial(b, bag[t]);
                yield return new WaitForSeconds(RandomRange(itiMin, itiMax));
            }
        }

        _session.summary.ended_at_utc = DateTime.UtcNow.ToString("o");
        FinalizeMetrics();
        SaveJson();
        Debug.Log("[SST-Semaforo] FIN. SSRT="+_session.summary.ssrt_ms+" ms  StopSucc="+_session.summary.stop_success_rate.ToString("F2"));
    }

    IEnumerator RunTrial(int blockIndex, string trialType)
    {
        _trialCounter++;
        var tr = new Trial
        {
            trial_id = _trialCounter,
            block_index = blockIndex,
            trial_type = trialType,
            ssd_ms = trialType=="stop" ? _currentSSD : 0,
            moved_on_go = false,
            rt_go_ms = -1,
            stop_beeped = false,
            stop_success = false,
            rt_stop_ms = -1
        };

        // Preparación de semáforo
        if (luzVerde) luzVerde.SetActive(false);
        if (luzRoja)  luzRoja.SetActive(false);
        yield return null;

        // Señal GO
        long onset = NowMs();
        tr.stim_onset_ms = (int)(onset - _t0ms);
        if (luzVerde) luzVerde.SetActive(true);
        runner.setGoGate(true); // puede moverse si mantiene Space

        bool isStop = (trialType=="stop");
        bool beepHecho = false;
        long beepTime = onset + tr.ssd_ms;

        bool goRTtaken = false;
        bool stopSuccessEvaluated = false;
        long stopSuccessDeadline = 0;

        while (NowMs() - onset < stimDurationMs)
        {
            long now = NowMs();

            // RT-Go: primer instante en que supera velocidad umbral
            if (!goRTtaken && runner.CurrentSpeed() > moveSpeedThreshold)
            {
                int rt = (int)(now - onset);
                if (rt >= rtMinMs)
                {
                    tr.moved_on_go = true;
                    tr.rt_go_ms = rt;
                    _rtGo.Add(rt);
                    goRTtaken = true;
                    _nGo++;
                }
            }

            // Lanza Stop (beep + luz roja)
            if (isStop && !beepHecho && now >= beepTime)
            {
                beepHecho = true;
                tr.stop_beeped = true;
                if (stopBeep) stopBeep.Play();
                if (luzRoja) { luzRoja.SetActive(true); }
                if (luzVerde) luzVerde.SetActive(false);
                // Nota: NO bloqueamos al runner. Debe decidir soltar Space.
                stopSuccessDeadline = now + stopSuccessWindowMs;
            }

            // Evaluar éxito de Stop: caer por debajo del umbral antes del deadline
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

        // Fin del trial
        runner.setGoGate(false); // aunque mantenga Space, ya no debería avanzar
        if (luzVerde) luzVerde.SetActive(false);
        if (luzRoja)  luzRoja.SetActive(false);

        // Staircase y contadores
        if (isStop)
        {
            _nStop++;
            if (tr.stop_success) { _nStopSucc++; _currentSSD = Mathf.Min(_currentSSD + ssdStepMs, ssdMaxMs); }
            else                 { _currentSSD = Mathf.Max(_currentSSD - ssdStepMs, ssdMinMs); }
            _ssdList.Add(tr.ssd_ms);
        }

        _session.trials.Add(tr);
    }

    void FinalizeMetrics()
    {
        _session.summary.n_trials = _session.trials.Count;
        _session.summary.stop_trials = _nStop;
        _session.summary.go_trials = _session.trials.Count - _nStop;
        _session.summary.stop_success_rate = _nStop>0 ? (float)_nStopSucc/_nStop : 0f;

        int rtMed = _rtGo.Count>0 ? Median(_rtGo) : -1;
        float rtCV = CV(_rtGo);
        int ssdMean = _ssdList.Count>0 ? Mathf.RoundToInt((float)_ssdList.Average()) : -1;
        int ssrt = (rtMed>=0 && ssdMean>=0) ? Mathf.Max(0, rtMed - ssdMean) : -1;

        _session.summary.rt_go_median_ms = rtMed;
        _session.summary.rt_go_cv = rtCV;
        _session.summary.ssd_mean_ms = ssdMean;
        _session.summary.ssrt_ms = ssrt;
    }

    void SaveJson()
    {
        string json = JsonUtility.ToJson(_session, true);
        string path = System.IO.Path.Combine(Application.persistentDataPath, _session.summary.session_id + ".json");
        System.IO.File.WriteAllText(path, json);
        Debug.Log("[SST-Semaforo] Guardado: " + path);
    }

    // Helpers
    long NowMs()=> (long)(Time.realtimeSinceStartup*1000f);
    void Shuffle<T>(List<T> list){ for(int i=list.Count-1;i>0;i--){ int k=_rng.Next(i+1); (list[i],list[k])=(list[k],list[i]); } }
    float RandomRange(float a,float b){ return (float)(_rng.NextDouble()*(b-a)+a); }
    int Median(List<int> xs){ if(xs==null||xs.Count==0) return -1; var o=xs.OrderBy(v=>v).ToList(); int m=o.Count/2; return (o.Count%2==1)? o[m] : Mathf.RoundToInt((o[m-1]+o[m])/2f); }
    float CV(List<int> xs){ if(xs==null||xs.Count<2) return -1f; float mean=(float)xs.Average(); float v=0; foreach(var x in xs){ float d=x-mean; v+=d*d; } v/=(xs.Count-1); float sd=Mathf.Sqrt(v); return mean>0? sd/mean : -1f; }
}
