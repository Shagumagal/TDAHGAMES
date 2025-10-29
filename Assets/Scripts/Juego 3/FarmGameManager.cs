using UnityEngine;
using System.Collections;

public enum FarmPhase { Intro, Planner, Tools, Routine, Summary }

public class FarmGameManager : MonoBehaviour
{
    public GameObject sortPhaseGO;   // Contenedor de la fase (GO que tiene SortPhase)
    public SessionLogger logger;

    private IPhase current;
    private bool uiLock = false;

    void Awake()
    {
        if (!sortPhaseGO) sortPhaseGO = GameObject.Find("SortPhase");
        if (!logger) logger = FindObjectOfType<SessionLogger>();
        if (!sortPhaseGO) Debug.LogError("[Farm] No encuentro 'SortPhase' en la escena.");
    }

    void Start()
    {
        logger?.StartSession();
        StartCoroutine(BootAndStart());
    }

    IEnumerator BootAndStart()
    {
        uiLock = true;
        yield return null; // un frame
        current = sortPhaseGO ? sortPhaseGO.GetComponent<IPhase>() : null;
        current?.StartPhase();
        uiLock = false;
    }

    void Update()
    {
        if (uiLock) return;

        if (current != null)
        {
            current.Tick();
            if (current.IsDone)
            {
                var summary = current.GetSummary();
                var phaseName = summary.ContainsKey("phase_name")
                    ? summary["phase_name"].ToString()
                    : current.GetType().Name;

                logger?.AppendPhaseSummary(phaseName, summary);
                logger?.FlushToDisk();
                Debug.Log("[Farm] Fin " + phaseName + ". JSON guardado.");

                uiLock = true; // evita re-entradas
            }
        }
    }
}
