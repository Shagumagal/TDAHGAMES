using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

[System.Serializable] public class Trial {
  public string trial_type;   // "go" | "nogo"
  public bool responded;
  public int? response_time_ms;
  public bool correct;
}

[System.Serializable]
public class PlannedTrial
{
  public string trial_type;  // "go"|"nogo"
  public int prefabIndex;    // índice dentro del array asignado
}


public class GoNoGo3D_Essential : MonoBehaviour
{
  [Header("UI")]
  public TextMeshProUGUI infoText;
  [Header("3D")]
  public Transform stimAnchor;
  public GameObject henGoPrefab;
  public GameObject henNoGoPrefab;

  [Header("Params")]
  public int trials = 180;
  public float goRatio = 0.8f;          // 4:1
  public float stimMs = 800f;
  public float postWindowMs = 300f;
  public float itiMinMs = 1200f, itiMaxMs = 1800f;

  List<Trial> log = new();
  System.Diagnostics.Stopwatch sw = new();

  void Start()
  {
    infoText.text = "Regla: PRESIONA ESPACIO (o clic) solo con la gallina AMIGA.\nNo presiones con la gallina PROHIBIDA.\n\nPresiona ESPACIO para comenzar.";
  }

  void Update()
  {
    if (!sw.IsRunning && Input.GetKeyDown(KeyCode.Space)) StartCoroutine(Run());
  }

  IEnumerator Run()
  {
    infoText.text = ""; sw.Start();
    for (int i = 0; i < trials; i++)
    {
      bool isGo = Random.value < goRatio;
      var t = new Trial { trial_type = isGo ? "go" : "nogo" };

      // Instancia estímulo
      GameObject prefab = isGo ? henGoPrefab : henNoGoPrefab;
      var stim = Instantiate(prefab, stimAnchor.position, Quaternion.identity);
      FaceCamera(stim.transform);

      // Ventana de estímulo
      float t0 = Time.time;
      bool responded = false; int? rt = null;
      while ((Time.time - t0) * 1000f < stimMs)
      {
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
        {
          responded = true; rt = (int)((Time.time - t0) * 1000f); break;
        }
        yield return null;
      }

      Destroy(stim);

      // Ventana post-estímulo corta (opcional)
      if (!responded && postWindowMs > 0f)
      {
        float p0 = Time.time;
        while ((Time.time - p0) * 1000f < postWindowMs)
        {
          if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
          {
            responded = true; rt = (int)(stimMs + (Time.time - p0) * 1000f); break;
          }
          yield return null;
        }
      }

      // Evaluación
      bool correct;
      if (isGo) correct = responded;
      else correct = !responded;

      t.responded = responded;
      t.response_time_ms = rt;
      t.correct = correct;
      log.Add(t);

      // ITI
      float iti = Random.Range(itiMinMs, itiMaxMs) / 1000f;
      yield return new WaitForSeconds(iti);
    }
    sw.Stop();

    // (Opcional) Calcula métricas y muestra un resumen amistoso al final:
    var summary = ComputeSummary(log);
    infoText.text = $"Fin.\nComisión: {summary.commissionRate:P0}\nOmisión: {summary.omissionRate:P0}\nRT mediana: {summary.rtMedian} ms\nVar RT (CV): {summary.rtCv:0.00}";
  }

  void FaceCamera(Transform t)
  {
    var cam = Camera.main; if (!cam) return;
    t.LookAt(cam.transform); t.rotation = Quaternion.Euler(0, t.rotation.eulerAngles.y, 0);
  }

  // ---- métricas esenciales (local) ----
  (double commissionRate, double omissionRate, int rtMedian, double rtCv) ComputeSummary(List<Trial> trials)
  {
    int go = 0, nogo = 0, com = 0, omi = 0;
    var rtList = new List<int>();
    foreach (var tr in trials)
    {
      if (tr.trial_type == "go")
      {
        go++;
        if (!tr.responded) omi++;
        if (tr.correct && tr.response_time_ms.HasValue && tr.response_time_ms.Value >= 150 && tr.response_time_ms.Value <= 2000)
          rtList.Add(tr.response_time_ms.Value);
      }
      else
      {
        nogo++;
        if (tr.responded) com++;
      }
    }
    double commission = nogo > 0 ? (double)com / nogo : 0;
    double omission = go > 0 ? (double)omi / go : 0;
    int median = rtList.Count > 0 ? Median(rtList) : 0;
    double cv = rtList.Count > 1 ? StdDev(rtList) / Mean(rtList) : 0;
    return (commission, omission, median, cv);
  }

  int Median(List<int> x) { x.Sort(); int n = x.Count; return n == 0 ? 0 : (n % 2 == 1 ? x[n / 2] : (x[n / 2 - 1] + x[n / 2]) / 2); }
  double Mean(List<int> x) { double s = 0; foreach (var v in x) s += v; return x.Count == 0 ? 0 : s / x.Count; }
  double StdDev(List<int> x) { double m = Mean(x); double v = 0; foreach (var v0 in x) v += (v0 - m) * (v0 - m); v /= Mathf.Max(1, x.Count - 1); return System.Math.Sqrt(v); }
}

