using UnityEngine;
using System.Collections.Generic;

public class ToolsPhase : MonoBehaviour, IPhase
{
    private float start;
    private bool done;

    public void StartPhase()
    {
        start = Time.time;
        done = false;
        Debug.Log("[Tools] Start");
    }

    public void Tick()
    {
        if (Time.time - start > 5f) done = true; // stub
    }

    public bool IsDone => done;

    public Dictionary<string, object> GetSummary()
    {
        var d = new Dictionary<string, object>();
        d["search_time_ms_avg"] = 3000; // placeholder
        d["tool_misplacements"] = 0;
        d["wrong_tool_picks"] = 0;
        d["revisits_same_area"] = 0;
        return d;
    }
}
