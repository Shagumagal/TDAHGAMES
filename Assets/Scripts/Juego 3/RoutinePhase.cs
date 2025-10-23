using UnityEngine;
using System.Collections.Generic;

public class RoutinePhase : MonoBehaviour, IPhase
{
    private float start;
    private bool done;

    public void StartPhase()
    {
        start = Time.time;
        done = false;
        Debug.Log("[Routine] Start");
    }

    public void Tick()
    {
        if (Time.time - start > 5f) done = true; // stub
    }

    public bool IsDone => done;

    public Dictionary<string, object> GetSummary()
    {
        var d = new Dictionary<string, object>();
        d["prospective_hits_pct"] = 0.5f; // placeholder
        d["prospective_late_pct"] = 0.2f;
        d["prospective_misses_pct"] = 0.3f;
        d["checklist_compliance_pct"] = 0.8f;
        return d;
    }
}
