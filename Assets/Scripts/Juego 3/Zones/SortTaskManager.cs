using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class SortTaskManager : MonoBehaviour
{
    [System.Serializable]
    public class TargetBin
    {
        public BinZone bin;
        public string binId;    // redundante para seguridad
        public int required;    // cuántos objetos deben ir aquí
        public int placed;      // contador actual
    }

    public List<TargetBin> targets = new List<TargetBin>();
    public bool enforceGlobalOrder = false;   // si true, exige orden entre contenedores
    public List<string> globalOrder = new List<string>(); // p.ej. {"Sem", "Herr", "Agua"}

    // Métricas
    public float startTime;
    public float endTime;
    public int wrongBinCount = 0;     // objeto en contenedor incorrecto
    public int overfillCount = 0;     // excedió capacidad/required
    public int repickCount = 0;       // agarrar-soltar-reagarrar
    public int dropsOutsideCount = 0; // sueltos fuera de bins
    public List<string> eventLog = new List<string>();
    int globalOrderIndex = 0;

    void Start()
    {
        startTime = Time.time;
        Log("sort_phase_start");
        // Failsafe: si no setearon binId en lista, tomar del componente
        foreach (var t in targets)
        {
            if (t.bin != null && string.IsNullOrEmpty(t.binId))
                t.binId = t.bin.binId;
        }
        if (enforceGlobalOrder && (globalOrder == null || globalOrder.Count == 0))
        {
            // Si no se definió, toma el orden de targets
            globalOrder = targets.Select(t => t.binId).ToList();
        }
    }

    public void RegisterRePick()
    {
        repickCount++;
        Log("repick");
    }

    public void TryPlaceInBin(GrabbableItem item, BinZone bin)
    {
        if (item == null || bin == null || item.isPlaced) return;

        var tgt = targets.FirstOrDefault(t => t.bin == bin);
        if (tgt == null)
        {
            // Bin no contemplado en objetivos
            wrongBinCount++;
            Log($"wrong_bin_unknown item={item.itemId} want={item.binId} got={bin.binId}");
            FeedbackWrong(bin.transform.position);
            return;
        }

        // Validar orden global (si aplica)
        if (enforceGlobalOrder)
        {
            string expectedBinNow = globalOrder[Mathf.Clamp(globalOrderIndex, 0, globalOrder.Count - 1)];
            if (bin.binId != expectedBinNow)
            {
                wrongBinCount++;
                Log($"wrong_global_order item={item.itemId} bin={bin.binId} expectedBin={expectedBinNow}");
                FeedbackWrong(bin.transform.position);
                return;
            }
        }

        // Validar categoría
        if (item.binId != bin.binId)
        {
            wrongBinCount++;
            Log($"wrong_bin item={item.itemId} want={item.binId} got={bin.binId}");
            FeedbackWrong(bin.transform.position);
            return;
        }

        // Validar capacidad/requeridos
        if (tgt.placed >= tgt.required || tgt.placed >= bin.capacity)
        {
            overfillCount++;
            Log($"overfill bin={bin.binId} item={item.itemId}");
            FeedbackWrong(bin.transform.position);
            return;
        }

        // Snap + lock
        DoSnap(item, bin, tgt.placed);
        tgt.placed++;
        item.isPlaced = true;

        Log($"placed item={item.itemId} bin={bin.binId} idx={tgt.placed}/{tgt.required}");

        // Avance de orden global si ese bin quedó completo
        if (enforceGlobalOrder && tgt.placed >= tgt.required && bin.binId == globalOrder[globalOrderIndex])
        {
            globalOrderIndex = Mathf.Min(globalOrderIndex + 1, globalOrder.Count - 1);
            Log($"advance_global_order to={globalOrder[globalOrderIndex]}");
        }

        // ¿Completado?
        if (targets.All(t => t.placed >= t.required))
        {
            endTime = Time.time;
            Log("sort_phase_complete");
            OnCompleted();
        }
    }

    void DoSnap(GrabbableItem item, BinZone bin, int indexInBin)
    {
        var rb = item.GetComponent<Rigidbody>();
        if (rb) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; rb.isKinematic = true; }

        var col = item.GetComponent<Collider>();
        if (col) col.enabled = false;

        item.transform.position = bin.GetSnapPosition(indexInBin);
        item.transform.rotation = bin.snapArea.rotation;
    }

    public void RegisterDropOutside(GrabbableItem item, Vector3 at)
    {
        dropsOutsideCount++;
        Log($"drop_outside item={item?.itemId}");
        FeedbackWrong(at);
    }

    void OnCompleted()
    {
        var total = endTime - startTime;
        Log($"metrics total_time={total:F2} wrong_bin={wrongBinCount} overfill={overfillCount} repick={repickCount} drops_out={dropsOutsideCount}");
        // Aquí llama a tu guardado JSON / POST a API
    }

    void FeedbackWrong(Vector3 pos)
    {
        // TODO: HUD / beep / flash
    }

    void Log(string msg)
    {
        eventLog.Add($"{Time.time:F3}|{msg}");
        // Debug.Log(msg);
    }
}
