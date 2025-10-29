using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class PauseMenuAutoUI : MonoBehaviour
{
    [Header("Config")]
    public string mainMenuScene = "Menu";
    public KeyCode toggleKey = KeyCode.Escape;
    public Color overlayColor = new Color(0f, 0f, 0f, 0.6f);
    public Color cardColor    = new Color(0.12f, 0.13f, 0.17f, 0.95f);
    public Color accent       = new Color(0.36f, 0.78f, 0.96f, 1f);

    private GameObject pauseOverlay;
    private bool isPaused;

    void Start()
    {
        BuildPauseUI();
        HidePause();
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            if (isPaused) HidePause(); else ShowPause();
        }
    }

    void BuildPauseUI()
    {
        if (!HasEventSystem())
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var canvasGO = new GameObject("PauseCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // Overlay
        pauseOverlay = CreateUI<Image>("PauseOverlay", canvasGO.transform).gameObject;
        var ovImg = pauseOverlay.GetComponent<Image>();
        Stretch(ovImg.rectTransform); ovImg.color = overlayColor;

        // Card
        var card = CreateUI<Image>("PauseCard", pauseOverlay.transform);
        SetSize(card.rectTransform, 520, 280); AnchorCenter(card.rectTransform);
        card.color = cardColor; AddShadow(card.gameObject, new Color(0,0,0,0.6f), new Vector2(0,-2), 6);

        var v = card.gameObject.AddComponent<VerticalLayoutGroup>();
        v.childAlignment = TextAnchor.MiddleCenter; v.padding = new RectOffset(24,24,24,24); v.spacing = 14;

        CreateText("T", card.transform, "Pausa", 32, FontStyle.Bold, TextAnchor.MiddleCenter);

        // Botones
        var btns = new GameObject("Buttons").AddComponent<RectTransform>();
        btns.SetParent(card.transform, false);
        var vBtns = btns.gameObject.AddComponent<VerticalLayoutGroup>();
        vBtns.childAlignment = TextAnchor.MiddleCenter; vBtns.spacing = 12;

        CreateMenuButton(vBtns.transform, "Continuar", HidePause);
        CreateMenuButton(vBtns.transform, "Salir al Menú", ExitToMenu);
    }

    void ShowPause()
    {
        pauseOverlay.SetActive(true);
        Time.timeScale = 0f;
        AudioListener.pause = true;
        isPaused = true;
        Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
    }

    void HidePause()
    {
        if (pauseOverlay) pauseOverlay.SetActive(false);
        Time.timeScale = 1f;
        AudioListener.pause = false;
        isPaused = false;
    }

    void ExitToMenu()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(mainMenuScene);
    }

    // ==== Helpers ====
    static Font UiFont {
        get {
            Font f = null;
            try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch {}
            if (!f) { string[] c = { "Arial", "Helvetica", "DejaVu Sans", "Liberation Sans", "Noto Sans" }; try { f = Font.CreateDynamicFontFromOSFont(c, 16); } catch {} }
            return f;
        }
    }
    static bool HasEventSystem()
    {
#if UNITY_2023_1_OR_NEWER
        return UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null;
#else
        return Object.FindObjectOfType<EventSystem>() != null;
#endif
    }
    T CreateUI<T>(string name, Transform parent) where T : Component
    { var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(T)); go.transform.SetParent(parent, false); return go.GetComponent<T>(); }
    Text CreateText(string n, Transform p, string txt, int size, FontStyle style, TextAnchor align)
    { var t = CreateUI<Text>(n, p); t.text = txt; t.font = UiFont; t.fontSize = size; t.fontStyle = style; t.alignment = align; t.color = Color.white; t.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1; return t; }
    Button CreateMenuButton(Transform p, string label, UnityAction onClick)
    { var b = CreateButton(p, label, onClick); var le = b.gameObject.AddComponent<LayoutElement>(); le.minHeight = 52; le.preferredWidth = 380; StylePrimary(b); return b; }
    Button CreateButton(Transform p, string label, UnityAction onClick)
    {
        var img = CreateUI<Image>($"Btn_{label}", p); img.color = new Color(1,1,1,0.10f);
        var btn = img.gameObject.AddComponent<Button>(); btn.onClick.AddListener(onClick);
        var t = CreateText("Label", img.transform, label, 20, FontStyle.Bold, TextAnchor.MiddleCenter); Stretch(t.rectTransform);
        var colors = btn.colors; colors.highlightedColor = new Color(1,1,1,0.18f); colors.pressedColor = new Color(1,1,1,0.25f); colors.selectedColor = colors.highlightedColor; btn.colors = colors;
        var rt = img.rectTransform; SetSize(rt, 380, 48); AddShadow(img.gameObject, new Color(0,0,0,0.55f), new Vector2(0,-2), 4); return btn;
    }
    void StylePrimary(Button b)
    { var img = b.GetComponent<Image>(); img.color = new Color(accent.r, accent.g, accent.b, 0.28f); var c = b.colors; c.normalColor = img.color; c.highlightedColor = new Color(accent.r, accent.g, accent.b, 0.38f); c.pressedColor = new Color(accent.r, accent.g, accent.b, 0.50f); b.colors = c; }
    static void Stretch(RectTransform rt){ rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero; }
    static void AnchorCenter(RectTransform rt){ rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.anchoredPosition = Vector2.zero; }
    static void SetSize(RectTransform rt, float w, float h){ AnchorCenter(rt); rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, w); rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h); }
    static void AddShadow(GameObject go, Color c, Vector2 dist, int size){ var s = go.AddComponent<Shadow>(); s.effectColor = c; s.effectDistance = dist; }
}
