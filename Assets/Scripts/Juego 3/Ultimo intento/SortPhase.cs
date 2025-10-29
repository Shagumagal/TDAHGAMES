using UnityEngine;
using System.Collections.Generic;
using TMPro;

public class SortPhase : MonoBehaviour, IPhase
{
    [SerializeField] private DropZoneClassifier classifier; // arrástralo en el Inspector
    private bool started, done;
    private TextMeshProUGUI timerTMP;
    private float t0;

    void Awake()
    {
        if (!classifier) classifier = GetComponent<DropZoneClassifier>();
        if (!classifier) classifier = GetComponentInChildren<DropZoneClassifier>(true);
    }

    public void StartPhase()
    {
        var t = GameObject.Find("PhaseTimerText");
        if (t) timerTMP = t.GetComponent<TextMeshProUGUI>();

        if (!classifier)
        {
            Debug.LogError("[SortPhase] Falta DropZoneClassifier");
            done = true; 
            return;
        }

        classifier.StartClassifier();
        t0 = Time.time;

        started = true; 
        done = false;
        Debug.Log("[SortPhase] Start");
    }

    public void Tick()
    {
        if (!started || done || !classifier) return;

        classifier.Tick();
        if (timerTMP) timerTMP.text = $"Clasificar: {Time.time - t0:0.0}s";

        if (classifier.IsCompleted()) done = true;
    }

    public bool IsDone => done;

    public Dictionary<string, object> GetSummary()
    {
        var sum = classifier != null ? classifier.GetSummary() : new Dictionary<string, object>();
        sum["phase_name"] = "SortPhase";
        return sum;
    }
}
