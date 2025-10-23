using UnityEngine;

public class InputIdleTracker : MonoBehaviour
{
    public string phaseTag = "idle"; // "idle" o "task"

    private int clicks, spacePress;
    private float pathLen, lastX, lastY;
    private float t0;
    private int bursts; 
    private int burstCount; 
    private float lastInputTime;

    void OnEnable(){ ResetCounters(); }

    void Update()
    {
        // Movimiento del mouse durante periodos idle
        var mx = Input.mousePosition.x; 
        var my = Input.mousePosition.y;
        if (phaseTag == "idle")
        {
            if (t0 == 0) { t0 = Time.time; lastX = mx; lastY = my; }
            pathLen += Vector2.Distance(new Vector2(mx, my), new Vector2(lastX, lastY));
            lastX = mx; lastY = my;
        }

        // Inputs (click/espacio) solo si estamos en idle
        bool inputNow = false;
        if (Input.GetMouseButtonDown(0)) { if (phaseTag == "idle") clicks++; inputNow = true; }
        if (Input.GetKeyDown(KeyCode.Space)) { if (phaseTag == "idle") spacePress++; inputNow = true; }

        // Ráfagas (≥3 inputs en 300 ms)
        if (inputNow)
        {
            float dt = Time.time - lastInputTime;
            lastInputTime = Time.time;
            if (dt <= 0.3f) { burstCount++; if (burstCount == 3) { bursts++; burstCount = 0; } }
            else burstCount = 1;
        }
    }

    public void SetPhase(string tag){ phaseTag = tag; }

    public void ResetCounters()
    {
        clicks = 0; 
        spacePress = 0; 
        pathLen = 0; 
        t0 = 0; 
        bursts = 0; 
        burstCount = 0; 
        lastInputTime = 0;
    }

    public (float clickRate, float spaceRate, float mousePathPxPerSec, int burstsCount) GetSummary()
    {
        float dur = Mathf.Max(0.001f, Time.time - t0);
        return (clicks / dur, spacePress / dur, pathLen / dur, bursts);
    }
}
