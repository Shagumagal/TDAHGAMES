using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class DropZoneClassifier : MonoBehaviour
{
    // ===== Config pública =====
    public string[] itemNames      = new[] { "Pala" };
    public string[] binNames       = new[] { "CanastoZone" };
    public string[] itemToBin      = new[] { "CanastoZone" };
    public int[]    requiredPerBin = new[] { 1 };
    public string[] globalOrder    = new[] { "Pala" };

    // Debug: si algo raro pasa, completa en X segundos para probar guardado de JSON
    public bool debugAutoComplete = false;
    public float debugAutoCompleteAfter = 6f;

    // ===== Ajustes =====
    public float rayDistance = 1000f;
    public float dragPlaneY  = 0f;  // fallback si no encontramos altura de bin

    // ===== Estado =====
    private Camera cam;
    private bool running, completed;
    private float tStart, tEnd;

    private readonly Dictionary<string, GameObject> items = new();
    private readonly Dictionary<string, Collider>   bins  = new();
    private readonly Dictionary<string, string>     itemExpectedBin = new();
    private readonly Dictionary<string, int>        itemPickCount = new();
    private readonly Dictionary<string, bool>       itemDelivered = new();
    private readonly Dictionary<Collider, string>   colliderToItemId = new();

    private readonly Dictionary<string, int> binDelivered = new();
    private int deliveredRequiredTotalTarget;
    private int deliveredRequiredTotal;

    // Orden global
    private int orderIdx = 0;
    private int orderCorrect = 0, orderWrong = 0;

    // Métricas A1-1
    private int wrongBinCount = 0, overfillCount = 0, dropsOutsideCount = 0;

    // Organización/búsqueda
    private int repickCount = 0;

    // Drag
    private GameObject dragging;
    private Vector3 dragOffset;
    private float currentDragY = 0f;

    public void StartClassifier()
    {
        cam = Camera.main;
        running = true; completed = false;
        tStart = Time.time; tEnd = 0f;

        items.Clear(); bins.Clear(); itemExpectedBin.Clear();
        itemPickCount.Clear(); itemDelivered.Clear(); binDelivered.Clear(); colliderToItemId.Clear();

        // Bins
        for (int i = 0; i < binNames.Length; i++)
        {
            var go = GameObject.Find(binNames[i]);
            if (!go) { Debug.LogError($"[DZC] Falta bin '{binNames[i]}'"); continue; }
            var col = go.GetComponent<Collider>();
            if (!col) col = go.AddComponent<BoxCollider>();
            col.isTrigger = true; // ok para highlight
            bins[binNames[i]] = col;
            binDelivered[binNames[i]] = 0;
        }

        // Items
        for (int i = 0; i < itemNames.Length; i++)
        {
            var id = itemNames[i];
            var go = GameObject.Find(id);
            if (!go) { Debug.LogError($"[DZC] Falta item '{id}'"); continue; }

            if (!go.GetComponentInChildren<Collider>()) go.AddComponent<BoxCollider>();
            var rb = go.GetComponent<Rigidbody>(); if (!rb) rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;

            foreach (var c in go.GetComponentsInChildren<Collider>(true))
                if (!colliderToItemId.ContainsKey(c)) colliderToItemId.Add(c, id);

            items[id] = go;
            itemPickCount[id] = 0;
            itemDelivered[id] = false;

            string expected = (i < itemToBin.Length) ? itemToBin[i] : binNames.First();
            itemExpectedBin[id] = expected;
        }

        deliveredRequiredTotalTarget = 0;
        for (int i = 0; i < Mathf.Min(binNames.Length, requiredPerBin.Length); i++)
            deliveredRequiredTotalTarget += Mathf.Max(0, requiredPerBin[i]);

        orderIdx = orderCorrect = orderWrong = 0;
        wrongBinCount = overfillCount = dropsOutsideCount = repickCount = 0;
        deliveredRequiredTotal = 0;

        Debug.Log("[DZC] Clasificación iniciada.");
    }

    public void Tick()
    {
        if (!running || completed) return;

        HandleDragAndDrop();

        // Autocomplete SOLO para probar que el manager guarda JSON
        if (debugAutoComplete && (Time.time - tStart) >= debugAutoCompleteAfter)
        {
            Debug.LogWarning("[DZC] DEBUG: AutoComplete activado → completando fase forzadamente.");
            running = false; completed = true; tEnd = Time.time;
            return;
        }

        if (deliveredRequiredTotal >= deliveredRequiredTotalTarget && deliveredRequiredTotalTarget > 0)
        {
            running = false; completed = true; tEnd = Time.time;
            Debug.Log("[DZC] Requeridos cumplidos. Fase completa.");
        }
    }

    public bool IsCompleted() => completed;

    public Dictionary<string, object> GetSummary()
    {
        int totalTimeMs = Mathf.RoundToInt(((completed ? tEnd : Time.time) - tStart) * 1000f);

        int requiredSum = deliveredRequiredTotalTarget;
        float binsCompliance = (requiredSum > 0) ? Mathf.Clamp01(deliveredRequiredTotal / (float)requiredSum) : 0f;

        float orderCompliance = (globalOrder != null && globalOrder.Length > 0)
            ? Mathf.Clamp01(orderCorrect / (float)globalOrder.Length)
            : 0f;

        var byBin = new Dictionary<string, object>();
        foreach (var b in binDelivered) byBin[b.Key] = b.Value;

        return new Dictionary<string, object>{
            ["phase_name"] = "SortPhase",
            ["total_time_ms"] = totalTimeMs,              // A1-6
            ["wrongBinCount"] = wrongBinCount,            // A1-1
            ["overfillCount"] = overfillCount,            // A1-1
            ["dropsOutsideCount"] = dropsOutsideCount,    // A1-1
            ["repickCount"] = repickCount,                // A1-5/7
            ["completed"] = completed,                    // A1-4
            ["bins_required_sum"] = requiredSum,
            ["bins_delivered_sum"] = deliveredRequiredTotal,
            ["compliance_bins_pct"] = binsCompliance,
            ["order_correct"] = orderCorrect,
            ["order_wrong"] = orderWrong,
            ["compliance_order_pct"] = orderCompliance,
            ["delivered_by_bin"] = byBin
        };
    }

    private void HandleDragAndDrop()
    {
        if (!cam) return;

        // PICK
        if (Input.GetMouseButtonDown(0) && dragging == null)
        {
            if (Physics.Raycast(cam.ScreenPointToRay(Input.mousePosition), out RaycastHit hit, rayDistance))
            {
                if (colliderToItemId.TryGetValue(hit.collider, out string itemId) &&
                    items.TryGetValue(itemId, out var root) &&
                    !IsDelivered(itemId))
                {
                    dragging = root;
                    dragOffset = root.transform.position - hit.point;

                    string expected = itemExpectedBin[itemId];
                    currentDragY = bins.TryGetValue(expected, out var expectedCol)
                        ? expectedCol.bounds.center.y
                        : root.transform.position.y;

                    itemPickCount[itemId] += 1;
                    if (itemPickCount[itemId] > 1) repickCount += 1;
                    Debug.Log($"[DZC] PICK '{itemId}' @Y={currentDragY:0.###}");
                }
            }
        }

        // DRAG
        if (Input.GetMouseButton(0) && dragging != null)
        {
            float planeY = currentDragY != 0f ? currentDragY : dragPlaneY;
            Plane plane = new Plane(Vector3.up, new Vector3(0, planeY, 0));
            if (plane.Raycast(cam.ScreenPointToRay(Input.mousePosition), out float dist))
            {
                Vector3 p = cam.ScreenPointToRay(Input.mousePosition).GetPoint(dist);
                dragging.transform.position = p + dragOffset;
            }
        }

        // DROP
        if (Input.GetMouseButtonUp(0) && dragging != null)
        {
            string itemId = dragging.name;

            // Alinear Y al bin esperado (garantiza intersección)
            string exp = itemExpectedBin[itemId];
            if (bins.TryGetValue(exp, out var expCol))
            {
                var p = dragging.transform.position;
                dragging.transform.position = new Vector3(p.x, expCol.bounds.center.y, p.z);
            }

            string binHit = DetectBinHit(dragging);
            Debug.Log($"[DZC] DROP '{itemId}' → binHit='{binHit ?? "null"}' expected='{exp}'");

            if (string.IsNullOrEmpty(binHit))
            {
                dropsOutsideCount++;
                dragging = null;
                return;
            }

            // Orden global (métrico)
            if (globalOrder != null && orderIdx < globalOrder.Length)
            {
                if (itemId == globalOrder[orderIdx]) { orderCorrect++; orderIdx++; }
                else { orderWrong++; }
            }

            if (binHit != exp)
            {
                wrongBinCount++;
                dragging = null;
                return;
            }

            // Capacidad
            int idx = System.Array.IndexOf(binNames, binHit);
            int capacity = (idx >= 0 && idx < requiredPerBin.Length) ? Mathf.Max(0, requiredPerBin[idx]) : 0;
            if (binDelivered[binHit] >= capacity && capacity > 0)
            {
                overfillCount++;
                dragging = null;
                return;
            }

            // Entrega correcta
            MarkDelivered(itemId);
            binDelivered[binHit] += 1;
            if (deliveredRequiredTotal < deliveredRequiredTotalTarget) deliveredRequiredTotal += 1;

            Debug.Log($"[DZC] ENTREGADO '{itemId}' en '{binHit}'. delivered={deliveredRequiredTotal}/{deliveredRequiredTotalTarget}");
            dragging.SetActive(false);
            dragging = null;
        }
    }

    private bool IsDelivered(string id) => itemDelivered.TryGetValue(id, out var ok) && ok;
    private void MarkDelivered(string id) { itemDelivered[id] = true; }

    // Detección robusta: Bounds → ComputePenetration → OverlapBox
    private string DetectBinHit(GameObject go)
    {
        var col = go.GetComponentInChildren<Collider>();
        if (!col) return null;

        Bounds itemB = col.bounds;

        string best = null;
        float bestDist = float.PositiveInfinity;

        foreach (var kv in bins)
        {
            Collider binCol = kv.Value;
            Bounds binB = binCol.bounds;

            // 1) Bounds con tolerancia
            Bounds expanded = binB;
            expanded.Expand(new Vector3(0.05f, 1.0f, 0.05f)); // +5cm XZ, +1m Y
            bool intersects = expanded.Contains(itemB.center) || expanded.Intersects(itemB);

            // 2) ComputePenetration (fallback)
            if (!intersects)
            {
                Vector3 dir; float dist;
                if (Physics.ComputePenetration(
                        col, col.transform.position, col.transform.rotation,
                        binCol, binCol.transform.position, binCol.transform.rotation,
                        out dir, out dist))
                {
                    intersects = true;
                }
            }

            // 3) OverlapBox (fallback extra; incluye triggers)
            if (!intersects)
            {
                var half = binB.extents * 1.05f;
                var hits = Physics.OverlapBox(binB.center, half, binCol.transform.rotation, ~0, QueryTriggerInteraction.Collide);
                foreach (var h in hits)
                {
                    if (h == col || h.transform.IsChildOf(go.transform)) { intersects = true; break; }
                }
            }

            if (intersects)
            {
                float d = Vector3.SqrMagnitude(itemB.center - binB.center);
                if (d < bestDist) { bestDist = d; best = kv.Key; }
            }
        }
        return best;
    }
}
