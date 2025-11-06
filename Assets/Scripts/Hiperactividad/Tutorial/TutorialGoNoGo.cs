using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.EventSystems;

public class TutorialGoNoGo : MonoBehaviour
{
    // --- Config ---
    const string PREF_KEY           = "GNG_TUTORIAL_SEEN";
    const float  PRACTICE_STIM_SEC  = 1.5f;
    const float  RT_WINDOW_SEC      = 1.2f;
    const KeyCode RESP_KEY          = KeyCode.Space;   // tecla de respuesta
    const KeyCode NEXT_KEY          = KeyCode.Return;  // ENTER para avanzar tutorial

    // Galería (fila frente a cámara)
    const float  GALLERY_DISTANCE   = 3.5f;
    const float  GALLERY_SPACING    = 2.2f;
    const float  GALLERY_Y_OFFSET   = 0.0f;

    // Spawn opcional
    const string NAME_SPAWN         = "StimSpawn";

    [Header("Forzar tutorial (útil en Editor)")]
    [SerializeField] private bool alwaysShowTutorial = false;

    // Estado
    bool practicePassed;

    // UI (TMP) – Enter-only
    Canvas canvas;
    CanvasGroup cgRoot;
    TMP_Text title, body;
    GameObject feedbackPanel; TMP_Text feedbackText;
    TMP_Text centerStimText;

    // Refs
    Transform stimSpawn;
    TDAHGame.GoNoGo3D_Essentials2 ctrl;

    void Awake()
    {
        ctrl = FindObjectOfType<TDAHGame.GoNoGo3D_Essentials2>();
        if (ctrl != null) ctrl.enabled = false;

        stimSpawn = GameObject.Find(NAME_SPAWN)?.transform;
    }

    IEnumerator Start()
    {
        // Saltar si ya se vio y no está forzado
        bool seen = PlayerPrefs.GetInt(PREF_KEY, 0) == 1;
        if (seen && !alwaysShowTutorial)
        {
            ForceHideAllUI();
            if (ctrl != null) { ctrl.enabled = true; ctrl.StartGameplayAfterTutorial(); }
            yield break;
        }

        BuildOverlayIfNeeded();

        // ---------- Páginas de reglas ----------
        yield return ShowPageEnterOnly(
            "Cómo jugar",
            "Cuando veas la 🟢 GALLINA (Go), presiona la BARRA ESPACIADORA.\n\nPresiona ENTER para continuar.");

        yield return ShowPageEnterOnly(
            "Regla No-Go",
            "Si aparece un estímulo 🟥 No-Go (por ejemplo, el zorro), NO PRESIONES nada.\n\nENTER para ver ejemplos.");

        // ---------- Galería (lee prefabs de TODOS los bloques) ----------
        var (goPrefabs, noGoPrefabs) = CollectStimFromController(maxPerType: 12, fromAllBlocks: true);

        if (goPrefabs.Count > 0)
        {
            yield return ShowGallery(
                goPrefabs, isGo: true,
                titleTxt: "Ejemplos Go",
                bodyTxt: "Estos son estímulos de tipo Go.\nCuando veas cualquiera de ellos: presiona ESPACIO.\n\nENTER para continuar."
            );
        }

        if (noGoPrefabs.Count > 0)
        {
            yield return ShowGallery(
                noGoPrefabs, isGo: false,
                titleTxt: "Ejemplos No-Go",
                bodyTxt: "Estos son estímulos de tipo No-Go.\nCon cualquiera de ellos: NO presiones.\n\nENTER para continuar."
            );
        }

        // ---------- Práctica (máx. 2 intentos) ----------
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
                    "Intentémoslo otra vez.\nGo = pulsa / No-Go = no pulses.\n\nENTER para reintentar.");
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
            // Toma 1 ejemplo por tipo (de todos los bloques si existen)
            var (gos, nogos) = CollectStimFromController(1, true);
            GameObject src = isGo ? gos.FirstOrDefault() : nogos.FirstOrDefault();

            yield return StartCoroutine(ShowStimulus(
                isGo,
                PRACTICE_STIM_SEC,
                RT_WINDOW_SEC,
                src,
                onHit:        () => { hits++; ShowFeedback("✔ ¡Bien!"); },
                onMiss:       () => {         ShowFeedback("✘ ¡Recuerda la gallina!"); },
                onCommission: () => { commissions++; ShowFeedback("✘ Espera al No-Go."); }
            ));

            yield return new WaitForSeconds(0.6f);
            HideFeedback();
        }

        practicePassed = (hits >= 4 && commissions <= 1);
    }

    IEnumerator ShowStimulus(bool isGo, float stimSec, float rtWindow,
        GameObject prefab,
        System.Action onHit, System.Action onMiss, System.Action onCommission)
    {
        GameObject clone = null;
        Camera cam = Camera.main ?? FindObjectOfType<Camera>();

        if (prefab != null)
        {
            clone = InstantiateStaticClone(prefab, cam, stimSpawn, GALLERY_DISTANCE, Vector3.zero);
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

    // ---------- Galería ----------
    (List<GameObject> goList, List<GameObject> noGoList)
    CollectStimFromController(int maxPerType = 6, bool fromAllBlocks = true, int blockIndex = 0)
    {
        var go = new List<GameObject>();
        var nogo = new List<GameObject>();
        if (ctrl == null) return (go, nogo);

        var blocks = typeof(TDAHGame.GoNoGo3D_Essentials2)
            .GetField("blocksSettings", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(ctrl) as System.Collections.IList;

        if (blocks == null || blocks.Count == 0) return (go, nogo);

        var indices = fromAllBlocks
            ? Enumerable.Range(0, blocks.Count)
            : new[] { Mathf.Clamp(blockIndex, 0, blocks.Count - 1) };

        foreach (int i in indices)
        {
            var block = blocks[i];
            var goArr   = block.GetType().GetField("goPrefabs")  ?.GetValue(block) as GameObject[];
            var nogoArr = block.GetType().GetField("noGoPrefabs")?.GetValue(block) as GameObject[];

            if (goArr   != null) go.AddRange(goArr.Where(x => x));
            if (nogoArr != null) nogo.AddRange(nogoArr.Where(x => x));
        }

        go   = go.Distinct().Take(maxPerType).ToList();
        nogo = nogo.Distinct().Take(maxPerType).ToList();
        return (go, nogo);
    }

    IEnumerator ShowGallery(List<GameObject> prefabs, bool isGo, string titleTxt, string bodyTxt)
    {
        // Overlay informativo
        ShowOverlay(titleTxt, bodyTxt);

        // Instancia en fila
        var clones = new List<GameObject>();
        Camera cam = Camera.main ?? FindObjectOfType<Camera>();

        if (prefabs.Count == 0)
        {
            // Fallback emoji si no hay prefabs
            ShowCenteredText(isGo ? "🟢🐔" : "🟥🦊");
        }
        else
        {
            // Centro base (StimSpawn o frente a la cámara)
            Vector3 center;
            Quaternion look;
            if (stimSpawn != null)
            {
                center = stimSpawn.position + Vector3.up * GALLERY_Y_OFFSET;
                look   = stimSpawn.rotation;
            }
            else
            {
                center = cam.transform.position + cam.transform.forward * GALLERY_DISTANCE + Vector3.up * GALLERY_Y_OFFSET;
                look   = Quaternion.LookRotation(cam.transform.forward, Vector3.up);
            }

            // Distribución horizontal centrada
            float totalWidth = (prefabs.Count - 1) * GALLERY_SPACING;
            Vector3 start = center - (cam.transform.right * (totalWidth / 2f));

            for (int i = 0; i < prefabs.Count; i++)
            {
                Vector3 offset = cam.transform.right * (i * GALLERY_SPACING);
                var clone = InstantiateStaticClone(prefabs[i], cam, stimSpawn, GALLERY_DISTANCE, offset);
                clones.Add(clone);
            }
        }

        // Espera ENTER para continuar
        while (!Input.GetKeyDown(NEXT_KEY)) yield return null;

        // Limpieza
        foreach (var c in clones) if (c) Destroy(c);
        HideCenteredText();
        HideOverlay();

        yield return null;
    }

    GameObject InstantiateStaticClone(GameObject src, Camera cam, Transform spawn, float distance, Vector3 extraOffset)
    {
        Vector3 pos; Quaternion rot;
        if (spawn != null)
        {
            pos = spawn.position + extraOffset;
            rot = spawn.rotation;
        }
        else
        {
            pos = cam.transform.position + cam.transform.forward * distance + extraOffset;
            rot = Quaternion.LookRotation(cam.transform.forward, Vector3.up);
        }

        var clone = Instantiate(src, pos, rot);

        // Congelar (sin físicas ni inputs propios)
        foreach (var rb in clone.GetComponentsInChildren<Rigidbody>(true))
        { rb.isKinematic = true; rb.useGravity = false; rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        foreach (var col in clone.GetComponentsInChildren<Collider>(true)) col.enabled = false;
        var anim = clone.GetComponent<Animator>(); if (anim) anim.applyRootMotion = false;
        var mover = clone.GetComponent("CreatureMover") as Behaviour; if (mover) mover.enabled = false;
        var input = clone.GetComponent("MovePlayerInput") as Behaviour; if (input) input.enabled = false;

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
        while (!Input.GetKeyDown(NEXT_KEY)) yield return null;  // espera ENTER
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
        t.font = TMP_Settings.defaultFontAsset; // evita el Arial.ttf obsoleto
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
}
