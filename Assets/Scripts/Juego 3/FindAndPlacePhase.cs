using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class FindAndPlacePhase : MonoBehaviour, IPhase
{
    private enum Sub { SEARCH, PLACE, DONE }

    // --- Config ---
    private readonly string[] targets = { "Pala", "Regadera", "Hoz" };
    private string dropZoneName = "CanastoZone"; // GameObject con BoxCollider isTrigger

    // --- Estado ---
    private Sub sub = Sub.SEARCH;
    private bool running = false, done = false;
    private float tStartSearch, tStartPlace;
    private Dictionary<string, float> tFound = new();
    private HashSet<string> remaining;
    private int wrongPicks = 0;
    private int wrongZoneDrops = 0;

    // Drag&Drop
    private GameObject dragging;
    private Vector3 dragOffset;
    private Collider dropZone;

    // --- UI ---
    private RectTransform panelTargets;         // "TargetsPanel"
    private TextMeshProUGUI[] targetLabels;     // hijos TMP del panel
    private TextMeshProUGUI timerTMP;           // "PhaseTimerText"

    public void StartPhase()
    {
        // UI refs
        var panelGO = GameObject.Find("TargetsPanel");
        if (panelGO) panelTargets = panelGO.GetComponent<RectTransform>();
        var labels = new List<TextMeshProUGUI>();
        if (panelTargets)
            foreach (Transform c in panelTargets)
                if (c.TryGetComponent(out TextMeshProUGUI tmp)) labels.Add(tmp);
        targetLabels = labels.ToArray();

        var tTimer = GameObject.Find("PhaseTimerText");
        if (tTimer) timerTMP = tTimer.GetComponent<TextMeshProUGUI>();

        // Drop zone
        var dz = GameObject.Find(dropZoneName);
        if (dz) dropZone = dz.GetComponent<Collider>();

        // Reset estado
        sub = Sub.SEARCH;
        running = true;
        done = false;
        wrongPicks = 0; wrongZoneDrops = 0;
        tFound.Clear();
        remaining = new HashSet<string>(targets);

        RefreshTargetsUI();
        tStartSearch = Time.time;  // tiempo de búsqueda arranca aquí
        Debug.Log("[FindAndPlace] Buscar: Pala, Regadera, Hoz");
    }

    public void Tick()
    {
        if (!running || done) return;

        // Timer visible
        if (timerTMP)
        {
            float t = sub == Sub.SEARCH ? (Time.time - tStartSearch) :
                     sub == Sub.PLACE  ? (Time.time - tStartPlace) : 0f;
            timerTMP.text = (sub == Sub.SEARCH ? "Buscar" : "Colocar") + $": {t:0.0}s";
        }

        if (sub == Sub.SEARCH)
        {
            if (Input.GetMouseButtonDown(0))
            {
                var cam = Camera.main; if (!cam) return;
                if (Physics.Raycast(cam.ScreenPointToRay(Input.mousePosition), out RaycastHit hit, 1000f))
                {
                    string id = hit.collider.gameObject.name;
                    if (remaining.Contains(id))
                    {
                        tFound[id] = Time.time - tStartSearch;
                        remaining.Remove(id);
                        hit.collider.gameObject.SetActive(false); // feedback
                        RefreshTargetsUI();

                        if (remaining.Count == 0)
                        {
                            // transicionar a PLACE
                            running = false;
                            // Reactivamos/respawneamos para poder arrastrar
                            RespawnTargetsForPlacement();
                            sub = Sub.PLACE;
                            running = true;
                            tStartPlace = Time.time;
                            Debug.Log("[FindAndPlace] Ahora coloca en CanastoZone.");
                        }
                    }
                    else
                    {
                        wrongPicks++;
                    }
                }
            }
        }
        else if (sub == Sub.PLACE)
        {
            HandleDragAndDrop();
        }
    }

    public bool IsDone => done;

    public Dictionary<string, object> GetSummary()
    {
        int tSearchMs = Mathf.RoundToInt((tStartPlace - tStartSearch) * 1000f);
        int tPlaceMs  = Mathf.RoundToInt((Time.time - tStartPlace) * 1000f);

        int tPala = tFound.ContainsKey("Pala") ? Mathf.RoundToInt(tFound["Pala"]*1000f) : -1;
        int tReg  = tFound.ContainsKey("Regadera") ? Mathf.RoundToInt(tFound["Regadera"]*1000f) : -1;
        int tHoz  = tFound.ContainsKey("Hoz") ? Mathf.RoundToInt(tFound["Hoz"]*1000f) : -1;

        return new Dictionary<string, object>{
            ["phase_name"] = "FindAndPlacePhase",
            ["search_time_ms"]   = tSearchMs,
            ["place_time_ms"]    = tPlaceMs,
            ["wrong_picks"]      = wrongPicks,
            ["wrong_zone_drops"] = wrongZoneDrops,
            ["t_Pala_found_ms"]      = tPala,
            ["t_Regadera_found_ms"]  = tReg,
            ["t_Hoz_found_ms"]       = tHoz
        };
    }

    private void RefreshTargetsUI()
    {
        if (!panelTargets || targetLabels == null || targetLabels.Length == 0) return;
        for (int i = 0; i < targetLabels.Length; i++)
        {
            if (i >= targets.Length) { targetLabels[i].text = ""; continue; }
            string id = targets[i];
            bool found = tFound.ContainsKey(id);
            targetLabels[i].text = found ? $"<color=#22C55E>✓</color> {id}" : $"• {id}";
        }
        panelTargets.gameObject.SetActive(true);
    }

    private void RespawnTargetsForPlacement()
    {
        foreach (var id in targets)
        {
            var src = GameObject.Find(id);
            GameObject go;
            if (src)
            {
                src.SetActive(true);
                go = src;
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = id;
                go.transform.position = new Vector3(Random.Range(-2f,2f), 1f, Random.Range(-2f,2f));
            }
            var rb = go.GetComponent<Rigidbody>(); if (!rb) rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
        }
    }

    private void HandleDragAndDrop()
    {
        var cam = Camera.main; if (!cam) return;

        if (Input.GetMouseButtonDown(0))
        {
            if (Physics.Raycast(cam.ScreenPointToRay(Input.mousePosition), out RaycastHit hit, 1000f))
            {
                if (IsTarget(hit.collider.gameObject.name))
                {
                    dragging = hit.collider.gameObject;
                    dragOffset = dragging.transform.position - hit.point;
                }
            }
        }

        if (Input.GetMouseButton(0) && dragging != null)
        {
            Plane ground = new Plane(Vector3.up, Vector3.zero);
            if (ground.Raycast(cam.ScreenPointToRay(Input.mousePosition), out float dist))
            {
                Vector3 p = cam.ScreenPointToRay(Input.mousePosition).GetPoint(dist);
                dragging.transform.position = p + dragOffset;
            }
        }

        if (Input.GetMouseButtonUp(0) && dragging != null)
        {
            bool correct = dropZone && dropZone.bounds.Contains(dragging.transform.position);
            if (correct)
            {
                dragging.SetActive(false);
                if (AllPlaced()) { running = false; done = true; }
            }
            else
            {
                wrongZoneDrops++;
            }
            dragging = null;
        }
    }

    private bool IsTarget(string name)
    {
        for (int i = 0; i < targets.Length; i++)
            if (targets[i] == name) return true;
        return false;
    }

    private bool AllPlaced()
    {
        foreach (var id in targets)
        {
            var go = GameObject.Find(id);
            if (go && go.activeInHierarchy) return false;
        }
        return true;
    }
}
