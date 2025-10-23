using UnityEngine;
using System.Collections;

public enum FarmPhase { Intro, Planner, Tools, Routine, Summary }

public class FarmGameManager : MonoBehaviour
{
    public GameObject plannerGO;
    public GameObject toolsGO;
    public GameObject routineGO;
    public SessionLogger logger;
    public bool useInstructions = true;
    public int countdownSeconds = 5;

    private IPhase current;
    private FarmPhase state = FarmPhase.Intro;
    private Coroutine pendingStart;
    private bool uiLock = false;   // <<< NUEVO

    void Awake()
    {
        if (!plannerGO) plannerGO = GameObject.Find("PlannerPhase");
        if (!logger)    logger    = FindObjectOfType<SessionLogger>();
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

        // (Quitamos hotkeys de test para no saltar fases)
        if (current != null)
        {
            current.Tick();
            if (current.IsDone)
            {
                logger?.AppendPhaseSummary("FindAndPlacePhase", current.GetSummary());
                logger?.FlushToDisk();
                Debug.Log("[Farm] Fin Fase 1. JSON guardado.");
                uiLock = true; // evita que vuelva a entrar
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
                title = "Fase 1 — Buscar y Colocar",
                body  = "Mira la lista arriba-derecha.\n1) Haz clic en Pala, Regadera y Hoz.\n2) Luego arrástralos al canasto."
            };
            yield return Instructions.ShowAndWait(data);
        }

        // 2) Countdown 5→1
        yield return CountdownOverlay.ShowAndWait(Mathf.Max(1, countdownSeconds), "¡Vamos!", null, null);

        // 3) Iniciar la fase
        current = plannerGO.GetComponent<IPhase>();
        current?.StartPhase();

        uiLock = false; // <<< desde aquí, Tick() corre y el timer avanza
    }
}
