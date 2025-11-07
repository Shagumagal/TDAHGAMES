using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

[ExecuteAlways]
[RequireComponent(typeof(FarmPackGameController))]
public class FarmPackUIBuilder : MonoBehaviour
{
    [Header("Auto build")]
    public bool buildOnPlay = true;

    [Header("HUD")]
    public float hudMargin = 16f;
    public int timerFontSize = 28;
    public int scoreFontSize = 24;
    public Color hudColor = Color.black;

    [Header("Start overlay")]
    public Vector2 overlaySize = new Vector2(700, 200);
    public Color overlayColor = new Color(0, 0, 0, 0.6f);
    public int startFontSize = 34;

    FarmPackGameController gc;

    void Awake()
    {
        gc = GetComponent<FarmPackGameController>();
        if (Application.isPlaying && buildOnPlay) BuildOrWire();
    }

    void OnValidate()
    {
        if (!Application.isPlaying) { gc = GetComponent<FarmPackGameController>(); }
    }

    [ContextMenu("Build / Wire UI")]
    public void BuildOrWire()
    {
        if (!gc) gc = GetComponent<FarmPackGameController>();

        // Canvas + EventSystem
        var canvas = FindObjectOfType<Canvas>();
        if (!canvas) canvas = CreateCanvas();
        EnsureEventSystem();

        // Raíz UI
        var root = canvas.transform.Find("HUD_Root");
        if (!root)
        {
            var go = new GameObject("HUD_Root", typeof(RectTransform));
            root = go.transform;
            root.SetParent(canvas.transform, false);
        }

        // ---------- HUD TOP-LEFT (Tiempo / Score) ----------
        var hud = root.Find("HUD_TopLeft");
        if (!hud)
        {
            var go = new GameObject("HUD_TopLeft", typeof(RectTransform));
            hud = go.transform;
            hud.SetParent(root, false);
            var rt = (RectTransform)hud;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(hudMargin, -hudMargin);
            rt.sizeDelta = new Vector2(600, 100);
        }

        // Timer
        TMP_Text timer = root.Find("HUD_TopLeft/TimerText")
            ? root.Find("HUD_TopLeft/TimerText").GetComponent<TMP_Text>()
            : null;
        if (!timer)
        {
            timer = CreateTMP("TimerText", (RectTransform)hud);
            var rt = timer.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = Vector2.zero;
        }
        timer.fontSize = timerFontSize;
        timer.color = hudColor;

        // Score
        TMP_Text score = root.Find("HUD_TopLeft/ScoreText")
            ? root.Find("HUD_TopLeft/ScoreText").GetComponent<TMP_Text>()
            : null;
        if (!score)
        {
            score = CreateTMP("ScoreText", (RectTransform)hud);
            var rt = score.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(0, - (timerFontSize + 6));
        }
        score.fontSize = scoreFontSize;
        score.color = hudColor;

        // ---------- START OVERLAY (central) ----------
        GameObject startPanel = root.Find("StartOverlay")
            ? root.Find("StartOverlay").gameObject
            : null;
        if (!startPanel)
        {
            startPanel = CreatePanel("StartOverlay", (RectTransform)root, overlaySize, overlayColor);
        }

        TMP_Text startText = root.Find("StartOverlay/StartText")
            ? root.Find("StartOverlay/StartText").GetComponent<TMP_Text>()
            : null;
        if (!startText)
        {
            startText = CreateTMP("StartText", startPanel.transform as RectTransform);
            startText.alignment = TextAlignmentOptions.Center;
            startText.fontSize = startFontSize;
            startText.color = Color.white;
            startText.text = "Pulsa ENTER para comenzar";
        }

        // ---------- Wiring al GameController ----------
        gc.hudTimerText  = timer;
        gc.hudScoreText  = score;
        gc.startPanel    = startPanel;
        gc.startText     = startText;

        // Asegura que no tape al empezar (tu GC lo muestra cuando toque)
        startPanel.SetActive(false);

        // Orden: HUD arriba
        hud.SetAsLastSibling();
        startPanel.transform.SetAsFirstSibling();

        Debug.Log("[FarmPackUIBuilder] HUD top-left + overlay listos y conectados.");
    }

    // ---------------- helpers ----------------
    Canvas CreateCanvas()
    {
        var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        var s = go.GetComponent<CanvasScaler>();
        s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = new Vector2(1920, 1080);
        return c;
    }

    void EnsureEventSystem()
    {
        if (!FindObjectOfType<EventSystem>())
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    TMP_Text CreateTMP(string name, RectTransform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var txt = go.GetComponent<TextMeshProUGUI>();
        txt.raycastTarget = false;
        txt.enableWordWrapping = false;
        return txt;
    }

    GameObject CreatePanel(string name, RectTransform parent, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>(); img.color = color;
        return go;
    }
}
