using UnityEngine;
using System.Collections;

public enum FarmPhase { Intro, Planner, Tools, Routine, Summary }

public class FarmGameManager : MonoBehaviour
{
    public GameObject plannerGO;   // Contenedor de la fase (puede ser "SortPhase" o "PlannerPhase")
    public GameObject toolsGO;
    public GameObject routineGO;
    public SessionLogger logger;
    public bool useInstructions = true;
    public int countdownSeconds = 5;

    private IPhase current;
    private FarmPhase state = FarmPhase.Intro;
    private Coroutine pendingStart;
    private bool uiLock = false;

    void Awake()
    {
        // Busca primero "SortPhase"; si no, cae a "PlannerPhase"
        if (!plannerGO)
        {
            var sort = GameObject.Find("SortPhase");
            plannerGO = sort ? sort : GameObject.Find("PlannerPhase");
        }
        if (!plannerGO) Debug.LogError("[Farm] No encuentro 'SortPhase' en la escena.");
        if (!logger) logger = FindObjectOfType<SessionLogger>();
    }

    void Start()
    {
        logger?.StartSession();
        SetState(FarmPhase.Planner);
    }

    void Update()
    {
        // Bloquea hotkeys y evaluación mientras hay UI
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

                uiLock = true;
                // Opcional: desactiva contenedor al terminar para evitar toques accidentales
                // if (plannerGO) plannerGO.SetActive(false);
            }
        }
    }

    private void SetState(FarmPhase next)
    {
        state = next;

        if (plannerGO) plannerGO.SetActive(state == FarmPhase.Planner);
        if (toolsGO)   toolsGO.SetActive(false);
        if (routineGO) routineGO.SetActive(false);

        if (pendingStart != null) StopCoroutine(pendingStart);
        pendingStart = StartCoroutine(PhaseIntroThenStart());
    }

    private IEnumerator PhaseIntroThenStart()
    {
        uiLock = true;

        // 1) Instrucciones
        if (useInstructions)
        {
            var data = new InstructionData {
                title = "Fase — Clasificar en Canasto",
                body  = "Toma el objeto indicado y suéltalo en el canasto correcto.\nSigue el orden si se muestra."
            };
            yield return Instructions.ShowAndWait(data);
        }

        // 2) Countdown 5→1
        yield return CountdownOverlay.ShowAndWait(Mathf.Max(1, countdownSeconds), "¡Vamos!", null, null);

        // 3) Iniciar la fase
        current = plannerGO ? plannerGO.GetComponent<IPhase>() : null;
        current?.StartPhase();

        uiLock = false; // desde aquí, Tick() corre y el timer avanza
    }
}
