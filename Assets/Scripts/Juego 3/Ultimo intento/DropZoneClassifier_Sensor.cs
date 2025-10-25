using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// Sensor por bin (se adjunta al bin en runtime)
public class DZBinSensor : MonoBehaviour
{
    public System.Action<Collider, bool> OnSense;
    void OnTriggerEnter(Collider other) => OnSense?.Invoke(other, true);
    void OnTriggerExit(Collider other)  => OnSense?.Invoke(other, false);
}

public class DropZoneClassifier_Sensor : MonoBehaviour
{
    // ===== Config =====
    public string[] itemNames      = new[] { "Pala" };
    public string[] binNames       = new[] { "CanastoZone" };
    public string[] itemToBin      = new[] { "CanastoZone" };
    public int[]    requiredPerBin = new[] { 1 };
    public string[] globalOrder    = new[] { "Pala" };

    public bool  debugAutoComplete      = false; // ponlo en true para probar guardado
    public float debugAutoCompleteAfter = 3f;

    public float rayDistance = 1000f;
    public float dragPlaneY  = 0f;  // fallback

    // ===== Estado =====
    private Camera cam;
    private bool running, completed;
    private float tStart, tEnd;

    private readonly Dictionary<string, GameObject> items = new Dictionary<string, GameObject>();
    private readonly Dictionary<string, Collider>   bins  = new Dictionary<string, Collider>();
    private readonly Dictionary<string, string>     itemExpectedBin = new Dictionary<string, string>();
    private readonly Dictionary<string, int>        itemPickCount = new Dictionary<string, int>();
    private readonly Dictionary<string, bool>       itemDelivered = new Dictionary<string, bool>();
    private readonly Dictionary<Collider, string>   colliderToItemId = new Dictionary<Collider, string>();
    private readonly Dictionary<string, int>        binDelivered = new Dictionary<string, int>();
    private readonly Dictionary<string, HashSet<string>> itemsInsideBin = new Dictionary<string, HashSet<string>>();

    private int deliveredRequiredTotalTarget;
    private int deliveredRequiredTotal;

    private int orderIdx = 0, orderCorrect = 0, orderWrong = 0;
    // A1-1
    private int wrongBinCount = 0, overfillCount = 0, dropsOutsideCount = 0;
    // A1-5/7
    private int repickCount = 0;

    // Drag
    private GameObject dragging;
    private Vector3 dragOffset;
    private float currentDragY = 0f;

    // ===== API =====
    public void StartClassifier()
    {
        cam = Camera.main;
        running = true; completed = false;
        tStart = Time.time; tEnd = 0f;

        items.Clear(); bins.Clear(); itemExpectedBin.Clear();
        itemPickCount.Clear(); itemDelivered.Clear(); binDelivered.Clear();
        colliderToItemId.Clear(); itemsInsideBin.Clear();

        // --- BINS + SENSOR ---
        for (int i = 0; i < binNames.Length; i++)
        {
            var go = GameObject.Find(binNames[i]);
            if (!go) { Debug.LogError($"[DZC v2] Falta bin '{binNames[i]}'"); continue; }

            var col = go.GetComponent<Collider>();
            if (!col) col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;

            string binName = binNames[i]; // captura para lambda

            var sensor = go.GetComponent<DZBinSensor>();
            if (!sensor) sensor = go.AddComponent<DZBinSensor>();

            itemsInsideBin[binName] = new HashSet<string>();
            sensor.OnSense = (other, entering) =>
            {
                if (!colliderToItemId.TryGetValue(other, out string itemId)) return;
                var set = itemsInsideBin[binName];
                if (entering) set.Add(itemId); else set.Remove(itemId);
            };

            bins[binName] = col;
            binDelivered[binName] = 0;
        }

        // --- ITEMS ---
        for (int i = 0; i < itemNames.Length; i++)
        {
            var id = itemNames[i];
            var go = GameObject.Find(id);
            if (!go) { Debug.LogError($"[DZC v2] Falta item '{id}'"); continue; }

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

        // Target de requeridos
        deliveredRequiredTotalTarget = 0;
        for (int i = 0; i < Mathf.Min(binNames.Length, requiredPerBin.Length); i++)
            deliveredRequiredTotalTarget += Mathf.Max(0, requiredPerBin[i]);

        orderIdx = orderCorrect = orderWrong = 0;
        wrongBinCount = overfillCount = dropsOutsideCount = repickCount = 0;
        deliveredRequiredTotal = 0;

        Debug.Log("[DZC v2] SENSOR MODE ON");
    }

    public void Tick()
    {
        if (!running || completed) return;

        HandleDragAndDrop();

        if (debugAutoComplete && (Time.time - tStart) >= debugAutoCompleteAfter)
        {
            Debug.LogWarning("[DZC v2] DEBUG: AutoComplete → completando fase.");
            running = false; completed = true; tEnd = Time.time;
            return;
        }

        if (deliveredRequiredTotal >= deliveredRequiredTotalTarget && deliveredRequiredTotalTarget > 0)
        {
            running = false; completed = true; tEnd = Time.time;
            Debug.Log("[DZC v2] Requeridos cumplidos. Fase completa.");
        }
    }

    public bool IsCompleted() => completed;

    public Dictionary<string, object> GetSummary()
    {
        int totalTimeMs = Mathf.RoundToInt(((completed ? tEnd : Time.time) - tStart) * 1000f);
        int requiredSum = deliveredRequiredTotalTarget;
        float binsCompliance = (requiredSum > 0) ? Mathf.Clamp01(deliveredRequiredTotal / (float)requiredSum) : 0f;
        float orderCompliance = (globalOrder != null && globalOrder.Length > 0) ? Mathf.Clamp01(orderCorrect / (float)globalOrder.Length) : 0f;

        var byBin = new Dictionary<string, object>();
        foreach (var b in binDelivered) byBin[b.Key] = b.Value;

        return new Dictionary<string, object>{
            ["phase_name"] = "SortPhase",
            ["total_time_ms"] = totalTimeMs,
            ["wrongBinCount"] = wrongBinCount,
            ["overfillCount"] = overfillCount,
            ["dropsOutsideCount"] = dropsOutsideCount,
            ["repickCount"] = repickCount,
            ["completed"] = completed,
            ["bins_required_sum"] = requiredSum,
            ["bins_delivered_sum"] = deliveredRequiredTotal,
            ["compliance_bins_pct"] = binsCompliance,
            ["order_correct"] = orderCorrect,
            ["order_wrong"] = orderWrong,
            ["compliance_order_pct"] = orderCompliance,
            ["delivered_by_bin"] = byBin
        };
    }

    // ===== Drag & Drop =====
    private void HandleDragAndDrop()
    {
        if (!cam) return;

        // PICK
        if (Input.GetMouseButtonDown(0) && dragging == null)
        {
            if (Physics.Raycast(cam.ScreenPointToRay(Input.mousePosition), out RaycastHit hit, rayDistance))
            {
                if (colliderToItemId.TryGetValue(hit.collider, out string itemId) &&
                    items.TryGetValue(itemId, out var root) && !IsDelivered(itemId))
                {
                    dragging = root;
                    dragOffset = root.transform.position - hit.point;

                    string expected = itemExpectedBin[itemId];
                    currentDragY = bins.TryGetValue(expected, out var expectedCol)
                        ? expectedCol.bounds.center.y
                        : root.transform.position.y;

                    itemPickCount[itemId] += 1;
                    if (itemPickCount[itemId] > 1) repickCount += 1;
                    Debug.Log($"[DZC v2] PICK '{itemId}' @Y={currentDragY:0.###}");
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
            string expectedBin = itemExpectedBin[itemId];

            // Alinear Y al bin esperado
            if (bins.TryGetValue(expectedBin, out var expCol))
            {
                var p = dragging.transform.position;
                dragging.transform.position = new Vector3(p.x, expCol.bounds.center.y, p.z);
            }

            // ¿está dentro del bin esperado?
            bool insideExpected = itemsInsideBin.TryGetValue(expectedBin, out var set) && set.Contains(itemId);
            if (!insideExpected)
            {
                // ¿en otro bin?
                foreach (var kv in itemsInsideBin)
                {
                    if (kv.Key != expectedBin && kv.Value.Contains(itemId))
                    {
                        wrongBinCount++;
                        Debug.Log($"[DZC v2] DROP '{itemId}' en bin equivocado '{kv.Key}' (esperado '{expectedBin}')");
                        dragging = null; return;
                    }
                }
                dropsOutsideCount++;
                Debug.Log($"[DZC v2] DROP '{itemId}' fuera de bins (esperado '{expectedBin}')");
                dragging = null; return;
            }

            // Orden global (métrica)
            if (globalOrder != null && orderIdx < globalOrder.Length)
            {
                if (itemId == globalOrder[orderIdx]) { orderCorrect++; orderIdx++; }
                else { orderWrong++; }
            }

            // Capacidad requerida
            int idx = System.Array.IndexOf(binNames, expectedBin);
            int capacity = (idx >= 0 && idx < requiredPerBin.Length) ? Mathf.Max(0, requiredPerBin[idx]) : 0;
            if (binDelivered[expectedBin] >= capacity && capacity > 0)
            {
                overfillCount++;
                Debug.Log($"[DZC v2] OVERFILL en '{expectedBin}'");
                dragging = null; return;
            }

            // Entrega correcta
            itemDelivered[itemId] = true;
            binDelivered[expectedBin] += 1;
            if (deliveredRequiredTotal < deliveredRequiredTotalTarget) deliveredRequiredTotal += 1;

            Debug.Log($"[DZC v2] ENTREGADO '{itemId}' en '{expectedBin}'. delivered={deliveredRequiredTotal}/{deliveredRequiredTotalTarget}");
            dragging.SetActive(false);
            dragging = null;
        }
    }

    private bool IsDelivered(string id) => itemDelivered.TryGetValue(id, out var ok) && ok;
}
