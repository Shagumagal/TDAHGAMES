using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.EventSystems;

public class TutorialGoNoGo : MonoBehaviour
{
    // --- Config ---
    const string PREF_KEY          = "GNG_TUTORIAL_SEEN";
    const float  PRACTICE_STIM_SEC = 1.5f;
    const float  RT_WINDOW_SEC     = 1.2f;
    const KeyCode RESP_KEY         = KeyCode.Space;   // tecla de respuesta
    const KeyCode NEXT_KEY         = KeyCode.Return;  // ENTER para avanzar tutorial
    const float  SPAWN_DISTANCE    = 3.5f;
    const string NAME_GO           = "Gallina GO";
    const string NAME_NOGO         = "Gallina no go";
    const string NAME_SPAWN        = "StimSpawn";

    [Header("Forzar tutorial (útil en Editor)")]
    [SerializeField] private bool alwaysShowTutorial = false;

    // Estado
    bool practicePassed;

    // UI (TMP) – sin botones (Enter-only)
    Canvas canvas;
    CanvasGroup cgRoot;
    TMP_Text title, body;
    GameObject feedbackPanel; TMP_Text feedbackText;
    TMP_Text centerStimText;

    // Prefabs/refs práctica
    GameObject tmplGO, tmplNOGO;
    Transform stimSpawn;

    // Controlador real
    TDAHGame.GoNoGo3D_Essentials2 ctrl;

    void Awake()
    {
        ctrl = FindObjectOfType<TDAHGame.GoNoGo3D_Essentials2>();
        if (ctrl != null) ctrl.enabled = false;

        (tmplGO, tmplNOGO) = FindStimTemplates(NAME_GO, NAME_NOGO);
        stimSpawn = GameObject.Find(NAME_SPAWN)?.transform;
    }

    IEnumerator Start()
    {
        // Si ya se vio Y no estamos forzando mostrarlo, saltar al juego
        bool seen = PlayerPrefs.GetInt(PREF_KEY, 0) == 1;
        if (seen && !alwaysShowTutorial)
        {
            ForceHideAllUI();
            if (ctrl != null) { ctrl.enabled = true; ctrl.StartGameplayAfterTutorial(); }
            yield break;
        }

        BuildOverlayIfNeeded();

        // Página 1
        yield return ShowPageEnterOnly(
            "Cómo jugar",
            "Cuando veas la 🟢 GALLINA, presiona la BARRA ESPACIADORA.\n\nPresiona ENTER para continuar.");

        // Página 2
        yield return ShowPageEnterOnly(
            "Muy bien",
            "Si aparece el 🟥 ZORRO, NO PRESIONES nada.\n\nPresiona ENTER para practicar.");

        // Práctica (máx. 2 intentos)
        int attempts = 0; bool passed = false;
        while (attempts < 2 && !passed)
        {
            attempts++;
            ForceHideAllUI();                 // oculta overlays antes de practicar
            yield return StartCoroutine(RunPracticeRoutine());
            passed = practicePassed;

            if (!passed && attempts < 2)
            {
                yield return ShowPageEnterOnly(
                    "¡Casi!",
                    "Intentémoslo otra vez.\nGALLINA = pulsa / ZORRO = no pulses.\n\nENTER para reintentar.");
            }
        }

        // Marcar como visto
        PlayerPrefs.SetInt(PREF_KEY, 1);
        PlayerPrefs.Save();

        // Última página
        yield return ShowPageEnterOnly(
            "¡Listo!",
            "Empieza el juego real. Hazlo rápido y con cuidado.\n\nPresiona ENTER para comenzar.");

        // Arranque del juego
        ForceHideAllUI();
        if (ctrl != null) { ctrl.enabled = true; ctrl.StartGameplayAfterTutorial(); }
    }

    // ---------- Menú contextual: resetear bandera ----------
    [ContextMenu("Resetear Tutorial (PlayerPrefs)")]
    void ResetTutorialCtx()
    {
        PlayerPrefs.DeleteKey(PREF_KEY);
        PlayerPrefs.Save();
        Debug.Log("Tutorial Go/No-Go reseteado (PlayerPrefs eliminado).");
    }

    // ---------- Práctica ----------
    IEnumerator RunPracticeRoutine()
    {
        // 6 ensayos: 4 GO / 2 NO-GO
        List<bool> seq = new() { true, false, true, true, false, true };
        Shuffle(seq);

        int hits = 0, commissions = 0;

        foreach (bool isGo in seq)
        {
            yield return StartCoroutine(ShowStimulus(
                isGo,
                PRACTICE_STIM_SEC,
                RT_WINDOW_SEC,
                onHit:        () => { hits++; ShowFeedback("✔ ¡Bien!"); },
                onMiss:       () => {         ShowFeedback("✘ ¡Recuerda la gallina!"); },
                onCommission: () => { commissions++; ShowFeedback("✘ Espera al zorro."); }
            ));

            yield return new WaitForSeconds(0.6f);
            HideFeedback();
        }

        practicePassed = (hits >= 4 && commissions <= 1);
    }

    IEnumerator ShowStimulus(bool isGo, float stimSec, float rtWindow,
        System.Action onHit, System.Action onMiss, System.Action onCommission)
    {
        GameObject clone = null;
        Camera cam = Camera.main ?? FindObjectOfType<Camera>();

        if (tmplGO != null && tmplNOGO != null)
        {
            var src = isGo ? tmplGO : tmplNOGO;
            clone = InstantiateForPractice(src, cam);
        }
        else
        {
            ShowCenteredText(isGo ? "🟢🐔" : "🟥🦊");
        }

        float t = 0f; bool responded = false;

        while (t < stimSec)
        {
            t += Time.deltaTime;
            if (!responded && Input.GetKeyDown(RESP_KEY))
            {
                responded = true;
                if (isGo) onHit?.Invoke(); else onCommission?.Invoke();
                break;
            }
            yield return null;
        }

        if (isGo && !responded) onMiss?.Invoke();

        if (clone != null) Destroy(clone);
        HideCenteredText();
    }

    GameObject InstantiateForPractice(GameObject src, Camera cam)
    {
        Vector3 pos; Quaternion rot;
        if (stimSpawn != null) { pos = stimSpawn.position; rot = stimSpawn.rotation; }
        else if (cam != null)  { pos = cam.transform.position + cam.transform.forward * SPAWN_DISTANCE;
                                 rot = Quaternion.LookRotation(cam.transform.forward, Vector3.up); }
        else { pos = Vector3.zero; rot = Quaternion.identity; }

        var clone = Instantiate(src, pos, rot);

        foreach (var rb in clone.GetComponentsInChildren<Rigidbody>(true))
        { rb.isKinematic = true; rb.useGravity = false; rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        foreach (var col in clone.GetComponentsInChildren<Collider>(true)) col.enabled = false;

        clone.transform.localScale = src.transform.localScale;
        clone.SetActive(true);
        return clone;
    }

    // ---------- UI ----------
    void BuildOverlayIfNeeded()
    {
        // Canvas
        canvas = FindObjectOfType<Canvas>();
        if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            var goCanvas = new GameObject("TutorialCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = goCanvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = goCanvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
        }
        if (!FindObjectOfType<EventSystem>())
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        // Root con CanvasGroup
        var root = new GameObject("TutorialRoot", typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(canvas.transform, false);
        cgRoot = root.GetComponent<CanvasGroup>();
        cgRoot.alpha = 1f; cgRoot.blocksRaycasts = true; cgRoot.interactable = true;

        // Fondo
        var overlay = NewUI("Overlay", root.transform);
        var overlayImg = overlay.AddComponent<Image>();
        overlayImg.color = new Color(0f, 0f, 0f, 0.55f);
        FullRect(overlay);

        // Card
        var card = NewUI("Card", root.transform);
        var cardImg = card.AddComponent<Image>();
        cardImg.color = new Color(0.07f, 0.09f, 0.15f, 0.72f);
        var rt = card.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(720, 420);

        // Título
        var tObj = NewUI("Title", card.transform);
        title = tObj.AddComponent<TextMeshProUGUI>();
        SetupTMP((TextMeshProUGUI)title, 40, TextAlignmentOptions.Top);
        SetRect(tObj, new Vector2(0.08f, 0.56f), new Vector2(0.92f, 0.93f));

        // Cuerpo
        var bObj = NewUI("Body", card.transform);
        body = bObj.AddComponent<TextMeshProUGUI>();
        SetupTMP((TextMeshProUGUI)body, 28, TextAlignmentOptions.Top);
        SetRect(bObj, new Vector2(0.08f, 0.24f), new Vector2(0.92f, 0.56f));

        // Feedback
        feedbackPanel = NewUI("FeedbackPanel", canvas.transform);
        var fImg = feedbackPanel.AddComponent<Image>();
        fImg.color = new Color(0f, 0f, 0f, 0.35f);
        FullRect(feedbackPanel); feedbackPanel.SetActive(false);

        var fTxtObj = NewUI("FeedbackText", feedbackPanel.transform);
        feedbackText = fTxtObj.AddComponent<TextMeshProUGUI>();
        SetupTMP((TextMeshProUGUI)feedbackText, 60, TextAlignmentOptions.Center);
        FullRect(fTxtObj);
    }

    IEnumerator ShowPageEnterOnly(string t, string b)
    {
        ShowOverlay(t, b);

        // Espera ENTER
        while (!Input.GetKeyDown(NEXT_KEY)) yield return null;
        HideOverlay();
    }

    void ShowOverlay(string t, string b)
    {
        if (canvas != null) canvas.gameObject.SetActive(true);
        if (cgRoot != null) { cgRoot.alpha = 1f; cgRoot.blocksRaycasts = true; cgRoot.interactable = true; }

        title.text = t;
        body.text  = b;
    }

    void HideOverlay()
    {
        if (cgRoot == null) return;
        cgRoot.alpha = 0f; cgRoot.blocksRaycasts = false; cgRoot.interactable = false;
    }

    void ForceHideAllUI()
    {
        HideOverlay();
        if (centerStimText != null) centerStimText.gameObject.SetActive(false);
        if (feedbackPanel   != null) feedbackPanel.SetActive(false);
    }

    void ShowCenteredText(string txt)
    {
        if (centerStimText == null)
        {
            var go = NewUI("StimText", canvas.transform);
            centerStimText = go.AddComponent<TextMeshProUGUI>();
            SetupTMP((TextMeshProUGUI)centerStimText, 96, TextAlignmentOptions.Center);
            SetRect(go, new Vector2(0.2f, 0.35f), new Vector2(0.8f, 0.65f));
        }
        canvas.gameObject.SetActive(true);
        centerStimText.text = txt;
        title.text = ""; body.text = "";
    }

    void HideCenteredText()
    {
        if (centerStimText != null) centerStimText.gameObject.SetActive(false);
    }

    void ShowFeedback(string txt)
    {
        (feedbackText as TextMeshProUGUI).text = txt;
        feedbackPanel.SetActive(true);
    }

    void HideFeedback() => feedbackPanel.SetActive(false);

    // ---------- Helpers ----------
    GameObject NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    void SetupTMP(TextMeshProUGUI t, int size, TextAlignmentOptions align)
    {
        t.font = TMP_Settings.defaultFontAsset;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.enableWordWrapping = true;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
    }

    void SetRect(GameObject go, Vector2 min, Vector2 max)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = min; rt.anchorMax = max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
    void FullRect(GameObject go) => SetRect(go, Vector2.zero, Vector2.one);

    void Shuffle<T>(IList<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int j = Random.Range(i, list.Count);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    (GameObject, GameObject) FindStimTemplates(string goName, string nogoName)
    {
        GameObject go = null, nogo = null;

        var roots = SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var r in roots)
        {
            foreach (var t in r.GetComponentsInChildren<Transform>(true))
            {
                if (go == null   && t.name == goName)   go   = t.gameObject;
                if (nogo == null && t.name == nogoName) nogo = t.gameObject;
                if (go != null && nogo != null) break;
            }
            if (go != null && nogo != null) break;
        }

        if (go == null || nogo == null)
        {
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (!t || !t.gameObject.scene.IsValid()) continue;
                if (go == null   && t.name == goName)   go   = t.gameObject;
                if (nogo == null && t.name == nogoName) nogo = t.gameObject;
                if (go != null && nogo != null) break;
            }
        }
        return (go, nogo);
    }
}

// Utilidad alternativa en caso de querer resetear por código externo
public static class GoNoGoTutorialUtil
{
    public static void ResetTutorial()
    {
        PlayerPrefs.DeleteKey("GNG_TUTORIAL_SEEN");
        PlayerPrefs.Save();
        Debug.Log("Tutorial Go/No-Go reseteado.");
    }
}
