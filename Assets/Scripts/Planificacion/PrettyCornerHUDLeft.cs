// PrettyCornerHUDLeft.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

[ExecuteAlways]
[RequireComponent(typeof(FarmPackGameController))]
public class PrettyCornerHUDLeft : MonoBehaviour
{
    [Header("Auto build")]
    public bool buildOnPlay = true;

    [Header("Top-Left HUD")]
    public Vector2 margin = new Vector2(18, 18);
    public Vector2 panelSize = new Vector2(360, 96);
    public Color  panelColor = new Color(1f, 1f, 1f, 0.85f);
    public Color  borderColor = new Color(0f, 0f, 0f, 0.15f);
    public Color  textColor = Color.black;
    public int    timerFontSize = 28;
    public int    scoreFontSize = 24;

    private FarmPackGameController gc;

    void Awake()
    {
        gc = GetComponent<FarmPackGameController>();
        if (Application.isPlaying && buildOnPlay) BuildOrWire();
    }

    [ContextMenu("Build / Wire (Pretty Corner HUD Left)")]
    public void BuildOrWire()
    {
        if (!gc) gc = GetComponent<FarmPackGameController>();

        // Canvas + EventSystem
        var canvas = FindObjectOfType<Canvas>();
        if (!canvas) canvas = CreateCanvas();
        if (!FindObjectOfType<EventSystem>())
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        // Root
        var root = canvas.transform.Find("PrettyHUDLeftRoot");
        if (!root)
        {
            var go = new GameObject("PrettyHUDLeftRoot", typeof(RectTransform));
            root = go.transform;
            root.SetParent(canvas.transform, false);
            var rt = (RectTransform)root;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        // Panel arriba-izquierda
        var panel = root.Find("TopLeftPanel") as RectTransform;
        if (!panel)
        {
            var go = new GameObject("TopLeftPanel", typeof(RectTransform), typeof(Image));
            panel = go.GetComponent<RectTransform>();
            panel.SetParent(root, false);
        }
        panel.anchorMin = panel.anchorMax = new Vector2(0, 1);
        panel.pivot = new Vector2(0, 1);
        panel.sizeDelta = panelSize;
        panel.anchoredPosition = new Vector2(margin.x, -margin.y);

        var img = panel.GetComponent<Image>();
        img.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
        img.type   = Image.Type.Sliced;
        img.color  = panelColor;

        var outline = panel.GetComponent<Outline>() ?? panel.gameObject.AddComponent<Outline>();
        outline.effectColor = borderColor;
        outline.effectDistance = new Vector2(2f, -2f);

        var shadow = panel.GetComponent<Shadow>() ?? panel.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.25f);
        shadow.effectDistance = new Vector2(3, -3);

        var layout = panel.GetComponent<VerticalLayoutGroup>() ?? panel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 12, 12);
        layout.spacing = 6;
        layout.childAlignment = TextAnchor.UpperLeft;

        var fitter = panel.GetComponent<ContentSizeFitter>() ?? panel.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Timer
        TMP_Text timer = panel.Find("TimerText") ? panel.Find("TimerText").GetComponent<TMP_Text>() : null;
        if (!timer)
        {
            timer = CreateTMP("TimerText", panel);
            timer.enableWordWrapping = false;
        }
        timer.fontSize = timerFontSize;
        timer.color = textColor;
        timer.text = "Tiempo: 0 s";
        AddSoftShadow(timer);

        // Score
        TMP_Text score = panel.Find("ScoreText") ? panel.Find("ScoreText").GetComponent<TMP_Text>() : null;
        if (!score)
        {
            score = CreateTMP("ScoreText", panel);
            score.enableWordWrapping = false;
        }
        score.fontSize = scoreFontSize;
        score.color = textColor;
        score.text = "Ok: 0/0 · Err: 0";
        AddSoftShadow(score);

        // Wire al GameController
        gc.hudTimerText = timer;
        gc.hudScoreText = score;

        panel.SetAsLastSibling();
        Debug.Log("[PrettyCornerHUDLeft] HUD arriba-izquierda conectado.");
    }

    // helpers
    Canvas CreateCanvas()
    {
        var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var c = go.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay;
        var s = go.GetComponent<CanvasScaler>();
        s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = new Vector2(1920, 1080);
        return c;
    }

    TMP_Text CreateTMP(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var txt = go.GetComponent<TextMeshProUGUI>();
        txt.raycastTarget = false;
        txt.alignment = TextAlignmentOptions.Left;
        return txt;
    }

    void AddSoftShadow(TMP_Text txt)
    {
        var sh = txt.GetComponent<Shadow>() ?? txt.gameObject.AddComponent<Shadow>();
        sh.effectColor = new Color(0, 0, 0, 0.25f);
        sh.effectDistance = new Vector2(1.5f, -1.5f);
    }
}
