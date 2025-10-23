using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class PlannerPhase : MonoBehaviour, IPhase
{
    private enum Sub { SEARCH, PLACE }

    [Header("Objetivos (nombres EXACTOS en escena)")]
    [SerializeField] private string[] targets = { "Pala", "Regadera", "Hoz" };

    [Header("Zona de entrega")]
    [SerializeField] private string dropZoneName = "CanastoZone";
    [SerializeField] private Vector3 overlapPadding = new Vector3(0.02f, 0.10f, 0.02f);

    [Header("Flujo")]
    [Tooltip("true = 2 pasos (clic luego colocar). false = 1 paso (agarrar y soltar directo).")]
    public bool twoStepFlow = false; // ← por defecto 1 paso

    [Header("Integración grabber")]
    [SerializeField] private ObjectGrabber grabber; // arrastra tu grabber aquí por Inspector

    // --- Estado ---
    private Sub sub;
    private bool running, done;
    private float tStartSearch, tStartPlace;

    private readonly Dictionary<string, GameObject> objRef = new();
    private readonly Dictionary<string, float> tFound = new(); // tiempos BUSCAR (solo 2 pasos)
    private readonly HashSet<string> remaining = new();
    private readonly HashSet<string> placed = new();

    private int wrongPicks = 0;
    private int wrongZoneDrops = 0;

    // Zona
    private BoxCollider dropZone;

    // UI
    private RectTransform panelTargets;
    private TextMeshProUGUI[] targetLabels;
    private TextMeshProUGUI timerTMP;

    public void StartPhase()
    {
        Debug.Log("[Planner] Start");

        // UI refs
        panelTargets = GameObject.Find("TargetsPanel")?.GetComponent<RectTransform>();
        var labels = new List<TextMeshProUGUI>();
        if (panelTargets)
            foreach (Transform c in panelTargets)
                if (c.TryGetComponent(out TextMeshProUGUI tmp)) labels.Add(tmp);
        targetLabels = labels.ToArray();
        timerTMP = GameObject.Find("PhaseTimerText")?.GetComponent<TextMeshProUGUI>();

        // Zona
        var dzGO = GameObject.Find(dropZoneName);
        if (dzGO && dzGO.TryGetComponent(out BoxCollider bx)) { dropZone = bx; dropZone.isTrigger = true; }
        else { dropZone = null; Debug.LogWarning($"[Planner] No hay BoxCollider en '{dropZoneName}'."); }

        // Reset
        running = true; done = false; wrongPicks = 0;
        tFound.Clear(); remaining.Clear(); placed.Clear(); objRef.Clear();

        // Cache objetivos
        foreach (var id in targets)
        {
            var go = GameObject.Find(id);
            if (!go) { Debug.LogWarning($"[Planner] Falta '{id}' en escena."); continue; }

            objRef[id] = go;

            if (!go.TryGetComponent<Collider>(out var col))
                col = go.AddComponent<BoxCollider>();
            col.enabled = true;      // que siempre tenga collider

            if (!go.TryGetComponent<Rigidbody>(out var rb))
                rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;

            remaining.Add(id);
        }

        // Flujo
        if (twoStepFlow)
        {
            // Paso A: BUSCAR (no se puede agarrar)
            if (grabber) { if (grabber.IsHolding()) grabber.ForceRelease(); grabber.enabled = false; }
            sub = Sub.SEARCH; tStartSearch = Time.time;
        }
        else
        {
            // 1 paso: empezar en COLOCAR (sí se puede agarrar ya)
            if (grabber) { if (grabber.IsHolding()) grabber.ForceRelease(); grabber.enabled = true; }
            sub = Sub.PLACE; tStartPlace = Time.time;
        }

        RefreshTargetsUI();
    }

    public void Tick()
    {
        if (!running || done) return;

        if (timerTMP)
        {
            float t = sub == Sub.SEARCH ? (Time.time - tStartSearch) : (Time.time - tStartPlace);
            timerTMP.text = (sub == Sub.SEARCH ? "Buscar" : "Colocar") + $": {t:0.0}s";
        }

        if (sub == Sub.SEARCH) HandleSearchClick();
        else                   CheckPlacementOverlapBox();
    }

    public bool IsDone => done;

    public Dictionary<string, object> GetSummary()
    {
        int tSearchMs = twoStepFlow ? Mathf.RoundToInt((tStartPlace - tStartSearch) * 1000f) : 0;
        int tPlaceMs  = Mathf.RoundToInt((Time.time - tStartPlace) * 1000f);

        int tPala = tFound.ContainsKey("Pala") ? Mathf.RoundToInt(tFound["Pala"]*1000f) : -1;
        int tReg  = tFound.ContainsKey("Regadera") ? Mathf.RoundToInt(tFound["Regadera"]*1000f) : -1;
        int tHoz  = tFound.ContainsKey("Hoz") ? Mathf.RoundToInt(tFound["Hoz"]*1000f) : -1;

        return new Dictionary<string, object>{
            ["phase_name"]           = "FindAndPlace",
            ["two_step_flow"]        = twoStepFlow,
            ["search_time_ms"]       = tSearchMs,
            ["place_time_ms"]        = tPlaceMs,
            ["wrong_picks"]          = wrongPicks,
            ["wrong_zone_drops"]     = wrongZoneDrops,
            ["t_Pala_found_ms"]      = tPala,
            ["t_Regadera_found_ms"]  = tReg,
            ["t_Hoz_found_ms"]       = tHoz
        };
    }

    // ---------- Buscar (solo si twoStepFlow = true) ----------
    private void HandleSearchClick()
    {
        if (!twoStepFlow) return;
        if (!Input.GetMouseButtonDown(0)) return;
        var cam = Camera.main; if (!cam) return;

        if (Physics.Raycast(cam.ScreenPointToRay(Input.mousePosition), out RaycastHit hit, 1000f))
        {
            string id = hit.collider.gameObject.name;

            if (remaining.Contains(id))
            {
                tFound[id] = Time.time - tStartSearch;
                remaining.Remove(id);

                // feedback: grisear y deshabilitar collider para que no vuelvan a contarse en buscar
                var go = objRef[id];
                var rend = go ? go.GetComponentInChildren<Renderer>() : null;
                if (rend && rend.material.HasProperty("_Color")) rend.material.color = new Color(0.75f,0.75f,0.75f);
                var col = go ? go.GetComponent<Collider>() : null;
                if (col) col.enabled = false;

                RefreshTargetsUI();

                if (remaining.Count == 0)
                {
                    // pasar a COLOCAR: reactivar colliders y habilitar grabber
                    foreach (var kv in objRef)
                    {
                        var c = kv.Value.GetComponent<Collider>();
                        if (c) c.enabled = true;
                    }
                    sub = Sub.PLACE; tStartPlace = Time.time;
                    if (grabber) grabber.enabled = true;
                    Debug.Log("[Planner] Ahora puedes agarrar y soltar en la zona.");
                }
            }
            else
            {
                wrongPicks++;
            }
        }
    }

    // ---------- Colocar (1 paso o 2º paso) ----------
    private void CheckPlacementOverlapBox()
    {
        if (!dropZone)
        {
            // Fallback simple por bounds si alguien quitó el BoxCollider
            var dz = GameObject.Find(dropZoneName)?.GetComponent<Collider>();
            if (!dz) return;
            Bounds zb = dz.bounds;
            foreach (var kv in objRef)
            {
                string id = kv.Key;
                if (placed.Contains(id)) continue;
                var go = kv.Value; if (!go || !go.activeInHierarchy) continue;
                var col = go.GetComponent<Collider>(); if (!col) continue;

                if (zb.Intersects(col.bounds)) MarkPlaced(go, id);
            }
            return;
        }

        Vector3 center = dropZone.transform.TransformPoint(dropZone.center);
        Vector3 halfExt = Vector3.Scale(dropZone.size * 0.5f, dropZone.transform.lossyScale) + overlapPadding;
        Quaternion rot = dropZone.transform.rotation;

        Collider[] hits = Physics.OverlapBox(center, halfExt, rot, ~0, QueryTriggerInteraction.Collide);

        foreach (var kv in objRef)
        {
            string id = kv.Key;
            if (placed.Contains(id)) continue;
            var go = kv.Value; if (!go || !go.activeInHierarchy) continue;
            var col = go.GetComponent<Collider>(); if (!col) continue;

            bool inside = false;
            for (int i = 0; i < hits.Length; i++)
                if (hits[i] == col) { inside = true; break; }

            if (inside) MarkPlaced(go, id);
        }
    }

    private void MarkPlaced(GameObject go, string id)
    {
        placed.Add(id);
        go.SetActive(false); // feedback
        RefreshTargetsUI(placedAsOk: true);

        if (AllPlaced())
        {
            running = false;
            done = true;
        }
    }

    // ---------- Helpers ----------
    private void RefreshTargetsUI(bool placedAsOk = false)
    {
        if (!panelTargets || targetLabels == null || targetLabels.Length == 0) return;

        for (int i = 0; i < targetLabels.Length; i++)
        {
            if (i >= targets.Length) { targetLabels[i].text = ""; continue; }
            string id = targets[i];

            bool ok = twoStepFlow ? tFound.ContainsKey(id) : placed.Contains(id); // 1 paso: marca al entregar
            targetLabels[i].text = ok ? $"[OK] {id}" : $"- {id}";
        }
        panelTargets.gameObject.SetActive(true);
    }

    private bool AllPlaced()
    {
        foreach (var id in targets) if (!placed.Contains(id)) return false;
        return true;
    }
}
