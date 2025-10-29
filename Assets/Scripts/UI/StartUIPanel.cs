using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Un solo archivo que: (1) Construye UI “pro” en tu Canvas (overlay + card + TMP + botón),
/// (2) Expone Show/Hide con fade y callback de Continuar.
/// Usar el botón del inspector “Build Start Panel (EditMode)” para generarlo en la escena.
/// </summary>
public class StartUIPanel : MonoBehaviour
{
    // Singleton práctico (opcional)
    public static StartUIPanel Instance { get; private set; }

    [Header("Refs autogeneradas")]
    public Canvas targetCanvas;
    public CanvasGroup panel;             // InstructionPanel/CanvasGroup
    public TextMeshProUGUI titleTMP;      // Title
    public TextMeshProUGUI bodyTMP;       // Body
    public Button continueButton;         // StartButton

    [Header("Teclas")]
    public KeyCode continueKey = KeyCode.Return;

    [Header("Fade (s)")]
    public float fadeIn = 0.25f;
    public float fadeOut = 0.25f;

    System.Action onContinue;
    bool built = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
        if (!Application.isPlaying) return;

        if (!built || !panel) BuildInScene(); // por si no se generó aún
        HideImmediate();
    }

    void Update()
    {
        if (!panel) return;
        if (panel.interactable && Input.GetKeyDown(continueKey))
            Continue();
    }

    // ---------- API PÚBLICA ----------
    public void Show(string title, string body, System.Action onContinueCB)
    {
        if (!panel) BuildInScene();
        if (titleTMP) titleTMP.text = title;
        if (bodyTMP)  bodyTMP.text  = body;
        onContinue = onContinueCB;

        continueButton.onClick.RemoveAllListeners();
        continueButton.onClick.AddListener(Continue);

        StopAllCoroutines();
        StartCoroutine(Fade(panel, 1f, fadeIn, true));
    }

    public void Hide()
    {
        if (!panel) return;
        StopAllCoroutines();
        StartCoroutine(Fade(panel, 0f, fadeOut, false));
    }

    
    [ContextMenu("Build Start Panel (EditMode)")]
    public void BuildInScene()
    {
        // Canvas & EventSystem
        if (!targetCanvas) targetCanvas = FindObjectOfType<Canvas>();
        if (!targetCanvas)
        {
            var goCanvas = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            targetCanvas = goCanvas.GetComponent<Canvas>();
            targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = goCanvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }
        if (!FindObjectOfType<UnityEngine.EventSystems.EventSystem>())
        {
            var es = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem),
                                    typeof(UnityEngine.EventSystems.StandaloneInputModule));
            es.transform.SetParent(targetCanvas.transform.parent, false);
        }

        // Raíz del panel
        var root = new GameObject("InstructionPanel", typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(targetCanvas.transform, false);
        panel = root.GetComponent<CanvasGroup>();
        panel.alpha = 0f; panel.interactable = false; panel.blocksRaycasts = false;

        // Overlay
        var overlay = new GameObject("Overlay", typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(root.transform, false);
        var rto = overlay.GetComponent<RectTransform>();
        rto.anchorMin = Vector2.zero; rto.anchorMax = Vector2.one;
        rto.offsetMin = Vector2.zero;  rto.offsetMax = Vector2.zero;
        var imgOverlay = overlay.GetComponent<Image>();
        imgOverlay.color = new Color(0,0,0,0.45f);
        imgOverlay.raycastTarget = false;

        // Card
        var card = new GameObject("Card", typeof(RectTransform), typeof(Image),
                                  typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        card.transform.SetParent(root.transform, false);
        var rtc = card.GetComponent<RectTransform>();
        rtc.anchorMin = rtc.anchorMax = new Vector2(0.5f, 0.5f);
        rtc.anchoredPosition = Vector2.zero;
        rtc.sizeDelta = new Vector2(680, 420);

        var cardImg = card.GetComponent<Image>();
        cardImg.color = new Color(0.07f, 0.09f, 0.15f, 0.72f); // #111827 a 72%

        var vlg = card.GetComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.padding = new RectOffset(32,32,28,24);
        vlg.spacing = 12;

        var csf = card.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Title TMP
        var titleGO = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGO.transform.SetParent(card.transform, false);
        titleTMP = titleGO.GetComponent<TextMeshProUGUI>();
        titleTMP.text = "Regla";
        titleTMP.enableAutoSizing = true; titleTMP.fontSizeMin = 28; titleTMP.fontSizeMax = 42;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.color = new Color32(249,250,251,255); // #F9FAFB
        titleTMP.raycastTarget = false;

        // Body TMP
        var bodyGO = new GameObject("Body", typeof(RectTransform), typeof(TextMeshProUGUI));
        bodyGO.transform.SetParent(card.transform, false);
        bodyTMP = bodyGO.GetComponent<TextMeshProUGUI>();
        bodyTMP.text = "Presiona <b>ESPACIO</b> con la gallina <b>AMIGA</b>.\nNo presiones con la gallina <b>PROHIBIDA</b>.";
        bodyTMP.enableAutoSizing = true; bodyTMP.fontSizeMin = 22; bodyTMP.fontSizeMax = 30;
        bodyTMP.alignment = TextAlignmentOptions.Center;
        bodyTMP.lineSpacing = 1.05f;
        bodyTMP.margin = new Vector4(8,0,8,0);
        bodyTMP.color = new Color32(229,231,235,255); // #E5E7EB
        bodyTMP.richText = true; bodyTMP.raycastTarget = false;

        // Botón
        var btnGO = new GameObject("StartButton", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(card.transform, false);
        var rtb = btnGO.GetComponent<RectTransform>();
        rtb.sizeDelta = new Vector2(0, 64);
        rtb.anchorMin = new Vector2(0.07f, 0.5f);
        rtb.anchorMax = new Vector2(0.93f, 0.5f);

        var btnImg = btnGO.GetComponent<Image>();
        btnImg.color = new Color32(59,130,246,255); // #3B82F6

        continueButton = btnGO.GetComponent<Button>();
        var colors = continueButton.colors;
        var c = btnImg.color;
        colors.highlightedColor = c * 1.1f;
        colors.pressedColor     = c * 0.9f;
        colors.disabledColor    = new Color(c.r, c.g, c.b, 0.5f);
        continueButton.colors = colors;

        // Texto botón
        var btnTextGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        btnTextGO.transform.SetParent(btnGO.transform, false);
        var bt = btnTextGO.GetComponent<TextMeshProUGUI>();
        bt.text = "Continuar (Enter)";
        bt.enableAutoSizing = true; bt.fontSizeMin = 20; bt.fontSizeMax = 30;
        bt.alignment = TextAlignmentOptions.Center;
        bt.color = new Color32(11,18,32,255); // #0B1220
        bt.raycastTarget = false;

        built = true;
    }

    // ---------- Internos ----------
    void Continue()
    {
        Hide();
        onContinue?.Invoke();
        onContinue = null;
    }

    void HideImmediate()
    {
        if (!panel) return;
        panel.alpha = 0f;
        panel.blocksRaycasts = false;
        panel.interactable = false;
    }

    System.Collections.IEnumerator Fade(CanvasGroup cg, float target, float dur, bool interactive)
    {
        if (!cg) yield break;
        cg.blocksRaycasts = interactive;
        cg.interactable   = interactive;
        float start = cg.alpha, t=0f;
        while (t < dur){ t += Time.deltaTime; cg.alpha = Mathf.Lerp(start, target, t/dur); yield return null; }
        cg.alpha = target;
    }
}
