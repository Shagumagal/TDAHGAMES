using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using System.Linq;
using System.Collections.Generic;

public class AutoMainMenuBuilder : MonoBehaviour
{
    // ============ Escenas ============
    [Header("Escenas (ajusta aquí)")]
    public string goNoGoScene = "SampleScene";   // Go/No-Go
    public string stopScene   = "Semaforo";      // Stop / Semáforo
    public string cptScene    = "Inatencion";    // CPT / Inatención (sin tilde)

    // ============ Fondo animado / imagen ============
    public enum BackgroundMode { None, StaticImage, AnimatedSprites, VideoClip, VideoURL }

    [Header("Fondo (elige 1 modo)")]
    public BackgroundMode bgMode = BackgroundMode.VideoClip;

    [Tooltip("Para StaticImage/AnimatedSprites")]
    public Sprite bgStaticSprite;
    [Tooltip("Frames para AnimatedSprites (simula GIF)")]
    public List<Sprite> bgAnimatedFrames;
    [Range(1f, 60f)] public float bgAnimFPS = 15f;

    [Tooltip("Para VideoClip")]
    public VideoClip bgVideoClip;
    [Tooltip("Para VideoURL (http/https/file:)")]
    public string bgVideoURL = "";

    public bool  bgVideoLoop = true;
    public bool  bgVideoMute = true;

    [Range(0f,1f)] public float bgDarken = 0.25f;
    public Color bgTint = new Color(0.02f, 0.02f, 0.03f, 0.65f);
    public bool useTintPulse = true;

    // ============ Estilo UI ============
    [Header("Estilo UI")]
    public string titulo = "TDAH Game — Menú Principal";
    public Color  cardColor = new Color(0.12f, 0.13f, 0.17f, 0.92f);
    public Color  accent = new Color(0.36f, 0.78f, 0.96f, 1f);

    // PlayerPrefs keys
    const string K_VOL  = "opt_masterVol";
    const string K_FS   = "opt_fullscreen";
    const string K_QL   = "opt_quality";
    const string K_RS   = "opt_resolution";
    const string K_SENS = "opt_mouseSens";

    // Estado overlays
    private GameObject optionsOverlay;
    private GameObject creditsOverlay;

    // UI opciones
    private Text qualityValueText, resValueText, sensValueText, volValueText;
    private Toggle fullscreenToggle;
    private Slider volSlider, sensSlider;
    private int qualityIndex;
    private int resIndex;
    private Resolution[] resolutionsDistinct;

    // Fondo refs
    private RawImage  videoRaw;
    private Image     imageBg;
    private VideoPlayer videoPlayer;
    private RenderTexture rt;
    private Image     tintOverlay;
    private float     frameTimer;
    private int       frameIndex;

    void Awake()
    {
        ApplySavedSettings(boot:true);
        EnsureCameraClear();
        BuildCanvasAndBackground();
        BuildMenuCard();
    }

    // ============ Settings ============
    void ApplySavedSettings(bool boot)
    {
        float vol = PlayerPrefs.GetFloat(K_VOL, 0.8f);
        AudioListener.volume = Mathf.Clamp01(vol);

        bool fs = PlayerPrefs.GetInt(K_FS, 1) == 1;
        if (boot) Screen.fullScreen = fs;

        int q = Mathf.Clamp(PlayerPrefs.GetInt(K_QL, QualitySettings.GetQualityLevel()), 0, QualitySettings.names.Length-1);
        QualitySettings.SetQualityLevel(q);
        qualityIndex = q;

        var all = Screen.resolutions;
        resolutionsDistinct = all
            .GroupBy(r => (r.width, r.height))
            .Select(g => g.OrderByDescending(r => r.refreshRateRatio.value).First())
            .OrderBy(r => r.width * r.height)
            .ToArray();

        int savedIdx = PlayerPrefs.GetInt(K_RS, -1);
        if (savedIdx >= 0 && savedIdx < resolutionsDistinct.Length) resIndex = savedIdx;
        else
        {
            var cur = Screen.currentResolution;
            resIndex = System.Array.FindIndex(resolutionsDistinct, r => r.width == cur.width && r.height == cur.height);
            if (resIndex < 0) resIndex = Mathf.Clamp(resolutionsDistinct.Length - 1, 0, int.MaxValue);
        }

        float sens = PlayerPrefs.GetFloat(K_SENS, 50f);
        PlayerPrefs.SetFloat(K_SENS, sens);
    }

    void EnsureCameraClear()
    {
        Camera main = Camera.main;
        if (!main)
        {
            var camGO = new GameObject("Main Camera");
            main = camGO.AddComponent<Camera>();
            camGO.tag = "MainCamera";
        }
        main.clearFlags = CameraClearFlags.SolidColor;
        main.backgroundColor = new Color(0.02f, 0.02f, 0.03f, 1f);
    }

    // ============ Canvas + Fondo ============
    void BuildCanvasAndBackground()
    {
        if (!HasEventSystem())
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // Capa de fondo
        var bgRoot = new GameObject("BackgroundLayer", typeof(RectTransform));
        bgRoot.transform.SetParent(canvasGO.transform, false);
        var bgRt = bgRoot.GetComponent<RectTransform>();
        Stretch(bgRt);

        // Video
        var videoGO = new GameObject("BgVideo", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        videoGO.transform.SetParent(bgRoot.transform, false);
        videoRaw = videoGO.GetComponent<RawImage>();
        Stretch(videoRaw.rectTransform);
        videoRaw.enabled = false;

        // Imagen
        var imgGO = new GameObject("BgImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imgGO.transform.SetParent(bgRoot.transform, false);
        imageBg = imgGO.GetComponent<Image>();
        Stretch(imageBg.rectTransform);
        imageBg.enabled = false; imageBg.preserveAspect = true;

        // Tinte overlay
        tintOverlay = CreateUI<Image>("BgTint", bgRoot.transform);
        Stretch(tintOverlay.rectTransform);
        var baseTint = new Color(bgTint.r, bgTint.g, bgTint.b, Mathf.Clamp01(bgTint.a + bgDarken));
        tintOverlay.color = baseTint;
        if (useTintPulse)
        {
            var pulser = tintOverlay.gameObject.AddComponent<BackgroundColorPulser>();
            pulser.baseColor = baseTint; pulser.speed = 0.13f; pulser.alphaAmplitude = 0.04f; pulser.hueShift = 0.012f;
        }

        // Modo de fondo
        switch (bgMode)
        {
            case BackgroundMode.VideoClip:
                if (bgVideoClip) SetupVideoPlayer(videoGO, clip: bgVideoClip, url: null);
                break;
            case BackgroundMode.VideoURL:
                if (!string.IsNullOrWhiteSpace(bgVideoURL)) SetupVideoPlayer(videoGO, clip: null, url: bgVideoURL);
                break;
            case BackgroundMode.AnimatedSprites:
                if (bgAnimatedFrames != null && bgAnimatedFrames.Count > 0) { imageBg.enabled = true; imageBg.sprite = bgAnimatedFrames[0]; }
                break;
            case BackgroundMode.StaticImage:
                if (bgStaticSprite) { imageBg.enabled = true; imageBg.sprite = bgStaticSprite; }
                break;
            case BackgroundMode.None:
            default: break;
        }
    }

    void SetupVideoPlayer(GameObject targetRawImageGO, VideoClip clip, string url)
    {
        videoRaw.enabled = true;
        int w = Mathf.Max(Screen.width, 1920), h = Mathf.Max(Screen.height, 1080);
        rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32); rt.Create();
        videoRaw.texture = rt;

        var vpGO = new GameObject("VideoPlayer");
        vpGO.transform.SetParent(targetRawImageGO.transform, false);
        videoPlayer = vpGO.AddComponent<VideoPlayer>();
        videoPlayer.playOnAwake = true; videoPlayer.isLooping = bgVideoLoop;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture; videoPlayer.targetTexture = rt;
        videoPlayer.audioOutputMode = bgVideoMute ? VideoAudioOutputMode.None : VideoAudioOutputMode.Direct;
        if (clip != null) videoPlayer.clip = clip; if (!string.IsNullOrWhiteSpace(url)) videoPlayer.url = url;
        videoPlayer.Prepare(); videoPlayer.prepareCompleted += (vp) => vp.Play();
    }

    // ============ Menú principal ============
    void BuildMenuCard()
    {
        var canvas = FindObjectOfType<Canvas>(); if (!canvas) return;

        // Card central
        var card = CreateUI<Image>("MainCard", canvas.transform);
        SetSize(card.rectTransform, 640, 620);
        AnchorCenter(card.rectTransform);
        card.color = cardColor;
        AddShadow(card.gameObject, new Color(0,0,0,0.55f), new Vector2(0, -2), 4);

        var vMain = card.gameObject.AddComponent<VerticalLayoutGroup>();
        vMain.childAlignment = TextAnchor.MiddleCenter;
        vMain.padding = new RectOffset(28,28,28,28);
        vMain.spacing = 16;

        // Título
        var title = CreateText("Title", card.transform, titulo, 38, FontStyle.Bold, TextAnchor.MiddleCenter);
        AddShadow(title.gameObject, new Color(0,0,0,0.6f), new Vector2(0, -1), 2);
        var leTitle = title.gameObject.AddComponent<LayoutElement>(); leTitle.preferredHeight = 70;

        // Subtítulo
        var subtitle = CreateText("Subtitle", card.transform, "Selecciona un juego", 20, FontStyle.Italic, TextAnchor.MiddleCenter);
        subtitle.color = new Color(1,1,1,0.85f);

        // Botones de juegos
        var buttonsWrap = new GameObject("Buttons").AddComponent<RectTransform>();
        buttonsWrap.SetParent(card.transform, false);
        var vButtons = buttonsWrap.gameObject.AddComponent<VerticalLayoutGroup>();
        vButtons.childAlignment = TextAnchor.MiddleCenter; vButtons.spacing = 12; vButtons.padding = new RectOffset(6,6,6,6);
        CreateMenuButton(vButtons.transform, "Jugar — Go/No-Go", () => SafeLoad(goNoGoScene));
        CreateMenuButton(vButtons.transform, "Jugar — Semáforo (Stop)", () => SafeLoad(stopScene));
        CreateMenuButton(vButtons.transform, "Jugar — Inatención (CPT)", () => SafeLoad(cptScene));

        // Separador
        var sep = CreateUI<Image>("Separator", card.transform);
        sep.color = new Color(1,1,1,0.10f);
        var seple = sep.gameObject.AddComponent<LayoutElement>(); seple.preferredHeight = 2; seple.minHeight = 2; seple.flexibleWidth = 9999;

        // Botones secundarios
        CreateMenuButton(card.transform, "Opciones", ToggleOptions);
        CreateMenuButton(card.transform, "Créditos", ToggleCredits);
        CreateMenuButton(card.transform, "Salir", QuitApp);

        // Overlays
        optionsOverlay  = BuildOptionsOverlay(canvas.transform);  optionsOverlay.SetActive(false);
        creditsOverlay  = BuildCreditsOverlay(canvas.transform);  creditsOverlay.SetActive(false);

        // Footer
        var foot = CreateText("Footer", canvas.transform, "© Proyecto TDAH — UNIVALLE", 14, FontStyle.Normal, TextAnchor.LowerCenter);
        foot.color = new Color(1,1,1,0.65f);
        var rtFoot = foot.rectTransform; rtFoot.anchorMin = rtFoot.anchorMax = new Vector2(0.5f, 0f); rtFoot.anchoredPosition = new Vector2(0, 22);
    }

    GameObject BuildOptionsOverlay(Transform parent)
    {
        var overlay = CreateUI<Image>("OptionsOverlay", parent);
        Stretch(overlay.rectTransform); overlay.color = new Color(0,0,0,0.60f);

        var win = CreateUI<Image>("OptionsWindow", overlay.transform);
        SetSize(win.rectTransform, 720, 580); AnchorCenter(win.rectTransform);
        win.color = new Color(0.11f,0.12f,0.16f,1f); AddShadow(win.gameObject, new Color(0,0,0,0.7f), new Vector2(0, -2), 6);

        var v = win.gameObject.AddComponent<VerticalLayoutGroup>();
        v.childAlignment = TextAnchor.UpperCenter; v.spacing = 14; v.padding = new RectOffset(24,24,24,24);

        CreateText("OptTitle", win.transform, "Opciones", 28, FontStyle.Bold, TextAnchor.MiddleCenter);

        volSlider = CreateSliderRow(win.transform, "Volumen maestro", 0f, 1f, AudioListener.volume, out volValueText, (val)=>{ AudioListener.volume = val; volValueText.text = Mathf.RoundToInt(val*100)+"%"; });
        fullscreenToggle = CreateToggleRow(win.transform, "Pantalla completa", Screen.fullScreen, (on)=>{ Screen.fullScreen = on; });

        qualityIndex = Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, QualitySettings.names.Length-1);
        CreateCyclerRow(win.transform, "Calidad gráfica", QualitySettings.names.ToList(), qualityIndex, out qualityValueText, (idx)=>{ qualityIndex = idx; QualitySettings.SetQualityLevel(idx); });

        var resLabels = resolutionsDistinct.Select(r => $"{r.width}×{r.height}").ToList();
        CreateCyclerRow(win.transform, "Resolución", resLabels, resIndex, out resValueText, (idx)=>{ resIndex = idx; });

        float sens = PlayerPrefs.GetFloat(K_SENS, 50f);
        sensSlider = CreateSliderRow(win.transform, "Sensibilidad (mouse)", 1f, 200f, sens, out sensValueText, (val)=>{ sensValueText.text = Mathf.RoundToInt(val).ToString(); });

        var row = CreateRow(win.transform); row.childAlignment = TextAnchor.MiddleRight;
        var btnGuardar = CreateButton(row.transform, "Guardar", ()=>{
            PlayerPrefs.SetFloat(K_VOL, volSlider.value);
            PlayerPrefs.SetInt(K_FS, fullscreenToggle.isOn ? 1 : 0);
            PlayerPrefs.SetInt(K_QL, qualityIndex);
            PlayerPrefs.SetInt(K_RS, Mathf.Clamp(resIndex,0,resolutionsDistinct.Length-1));
            PlayerPrefs.SetFloat(K_SENS, sensSlider.value);
            var r = resolutionsDistinct[Mathf.Clamp(resIndex,0,resolutionsDistinct.Length-1)];
            Screen.SetResolution(r.width, r.height, Screen.fullScreen);
            PlayerPrefs.Save(); ToggleOptions();
        }); StylePrimary(btnGuardar);

        var btnCerrar = CreateButton(row.transform, "Cerrar", ToggleOptions);

        volValueText.text  = Mathf.RoundToInt(volSlider.value*100)+"%";
        sensValueText.text = Mathf.RoundToInt(sensSlider.value).ToString();
        qualityValueText.text = QualitySettings.names[qualityIndex];
        var rr = resolutionsDistinct[Mathf.Clamp(resIndex,0,resolutionsDistinct.Length-1)];
        resValueText.text = rr.width + "×" + rr.height;

        return overlay.gameObject;
    }

    GameObject BuildCreditsOverlay(Transform parent)
    {
        var overlay = CreateUI<Image>("CreditsOverlay", parent);
        Stretch(overlay.rectTransform); overlay.color = new Color(0,0,0,0.60f);

        var win = CreateUI<Image>("CreditsWindow", overlay.transform);
        SetSize(win.rectTransform, 720, 560); AnchorCenter(win.rectTransform);
        win.color = new Color(0.11f,0.12f,0.16f,1f); AddShadow(win.gameObject, new Color(0,0,0,0.7f), new Vector2(0, -2), 6);

        var v = win.gameObject.AddComponent<VerticalLayoutGroup>();
        v.childAlignment = TextAnchor.UpperCenter; v.spacing = 10; v.padding = new RectOffset(24,24,24,24);

        CreateText("CredTitle", win.transform, "Créditos", 28, FontStyle.Bold, TextAnchor.MiddleCenter);

        // Contenido (ajústalo a tu gusto)
        CreateText("C1", win.transform, "Dirección del proyecto: Fabricio Mariscal", 18, FontStyle.Normal, TextAnchor.MiddleCenter);
        CreateText("C2", win.transform, "Desarrollo: Unity (C#), Web (React/TS)", 18, FontStyle.Normal, TextAnchor.MiddleCenter);
        CreateText("C3", win.transform, "Institución: UNIVALLE — Proyecto TDAH", 18, FontStyle.Normal, TextAnchor.MiddleCenter);
        CreateText("C4", win.transform, "Agradecimientos: Psicología Clínica y docentes colaboradores", 18, FontStyle.Italic, TextAnchor.MiddleCenter);

        var row = CreateRow(win.transform); row.childAlignment = TextAnchor.MiddleRight;
        var btnCerrar = CreateButton(row.transform, "Cerrar", ToggleCredits); StylePrimary(btnCerrar);

        return overlay.gameObject;
    }

    // ============ Acciones ============
    void ToggleOptions()  => optionsOverlay.SetActive(!optionsOverlay.activeSelf);
    void ToggleCredits()  => creditsOverlay.SetActive(!creditsOverlay.activeSelf);

    void QuitApp()
    {
#if UNITY_EDITOR
        Debug.Log("[Menú] Salir (Editor): deteniendo PlayMode.");
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void SafeLoad(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName)) { Debug.LogError("[Menú] Nombre de escena vacío."); return; }
        SceneManager.LoadScene(sceneName);
    }

    // ============ Update (sprites animados) ============
    void Update()
    {
        if (bgMode == BackgroundMode.AnimatedSprites && imageBg && imageBg.enabled && bgAnimatedFrames != null && bgAnimatedFrames.Count > 1)
        {
            frameTimer += Time.deltaTime; float frameDur = 1f / Mathf.Max(1f, bgAnimFPS);
            if (frameTimer >= frameDur) { frameTimer -= frameDur; frameIndex = (frameIndex + 1) % bgAnimatedFrames.Count; imageBg.sprite = bgAnimatedFrames[frameIndex]; }
        }
    }

    // ============ Helpers UI ============
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
        return FindObjectOfType<EventSystem>() != null;
#endif
    }
    T CreateUI<T>(string name, Transform parent) where T : Component
    { var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(T)); go.transform.SetParent(parent, false); return go.GetComponent<T>(); }
    Text CreateText(string n, Transform p, string txt, int size, FontStyle style, TextAnchor align)
    { var t = CreateUI<Text>(n, p); t.text = txt; t.font = UiFont; t.fontSize = size; t.fontStyle = style; t.alignment = align; t.color = Color.white; t.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1; return t; }
    Button CreateMenuButton(Transform p, string label, UnityAction onClick)
    { var b = CreateButton(p, label, onClick); var le = b.gameObject.AddComponent<LayoutElement>(); le.minHeight = 54; le.preferredWidth = 520; StylePrimary(b); return b; }
    Button CreateButton(Transform p, string label, UnityAction onClick)
    {
        var img = CreateUI<Image>($"Btn_{label}", p); img.color = new Color(1,1,1,0.10f);
        var btn = img.gameObject.AddComponent<Button>(); btn.onClick.AddListener(onClick);
        var t = CreateText("Label", img.transform, label, 20, FontStyle.Bold, TextAnchor.MiddleCenter); Stretch(t.rectTransform);
        var colors = btn.colors; colors.highlightedColor = new Color(1,1,1,0.18f); colors.pressedColor = new Color(1,1,1,0.25f); colors.selectedColor = colors.highlightedColor; btn.colors = colors;
        var rt = img.rectTransform; SetSize(rt, 520, 50); AddShadow(img.gameObject, new Color(0,0,0,0.55f), new Vector2(0, -2), 4); return btn;
    }
    void StylePrimary(Button b)
    { var img = b.GetComponent<Image>(); img.color = new Color(accent.r, accent.g, accent.b, 0.28f); var c = b.colors; c.normalColor = img.color; c.highlightedColor = new Color(accent.r, accent.g, accent.b, 0.38f); c.pressedColor = new Color(accent.r, accent.g, accent.b, 0.50f); b.colors = c; }
    HorizontalLayoutGroup CreateRow(Transform p)
    { var row = new GameObject("Row", typeof(RectTransform)).GetComponent<RectTransform>(); row.SetParent(p, false); var h = row.gameObject.AddComponent<HorizontalLayoutGroup>(); h.childAlignment = TextAnchor.MiddleLeft; h.spacing = 10; h.padding = new RectOffset(6,6,6,6); row.gameObject.AddComponent<LayoutElement>().preferredHeight = 48; row.gameObject.GetComponent<LayoutElement>().flexibleWidth = 1; return h; }
    Slider CreateSliderRow(Transform p, string label, float min, float max, float value, out Text valueText, System.Action<float> onChanged)
    {
        var row = CreateRow(p); CreateText("Label", row.transform, label, 18, FontStyle.Normal, TextAnchor.MiddleLeft);
        var slot = new GameObject("SliderSlot", typeof(RectTransform)).GetComponent<RectTransform>(); slot.SetParent(row.transform, false); slot.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        var bg = CreateUI<Image>("SliderBG", slot); Stretch(bg.rectTransform); bg.color = new Color(1,1,1,0.08f);
        var fillArea = CreateUI<RectTransform>("Fill Area", bg.transform); fillArea.anchorMin = new Vector2(0, 0.25f); fillArea.anchorMax = new Vector2(1, 0.75f); fillArea.offsetMin = new Vector2(10, 0); fillArea.offsetMax = new Vector2(-30, 0);
        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image)).GetComponent<Image>(); fill.transform.SetParent(fillArea, false); Stretch(fill.rectTransform); fill.color = accent;
        var handleSlideArea = CreateUI<RectTransform>("Handle Slide Area", bg.transform); handleSlideArea.anchorMin = new Vector2(0, 0); handleSlideArea.anchorMax = new Vector2(1, 1); handleSlideArea.offsetMin = new Vector2(10, 0); handleSlideArea.offsetMax = new Vector2(-10, 0);
        var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image)).GetComponent<Image>(); handle.transform.SetParent(handleSlideArea, false); SetSize(handle.rectTransform, 22, 22); handle.color = Color.white;
        var slider = bg.gameObject.AddComponent<Slider>(); slider.fillRect = fill.rectTransform; slider.handleRect = handle.rectTransform; slider.minValue = min; slider.maxValue = max; slider.value = Mathf.Clamp(value, min, max); slider.onValueChanged.AddListener(v => onChanged(v));
        var display = CreateText("Value", row.transform, "", 16, FontStyle.Bold, TextAnchor.MiddleRight); display.gameObject.AddComponent<LayoutElement>().preferredWidth = 90; valueText = display; return slider;
    }
    Toggle CreateToggleRow(Transform p, string label, bool initial, UnityAction<bool> onChanged)
    {
        var row = CreateRow(p); CreateText("Label", row.transform, label, 18, FontStyle.Normal, TextAnchor.MiddleLeft);
        var spacer = new GameObject("Spacer", typeof(RectTransform)).GetComponent<RectTransform>(); spacer.SetParent(row.transform, false); spacer.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        var bg = CreateUI<Image>("ToggleBG", row.transform); SetSize(bg.rectTransform, 40, 22); bg.color = new Color(1,1,1,0.08f);
        var check = new GameObject("Check", typeof(RectTransform), typeof(Image)).GetComponent<Image>(); check.transform.SetParent(bg.transform, false); SetSize(check.rectTransform, 16, 16); check.color = Color.white; check.rectTransform.anchoredPosition = new Vector2(-10, 0);
        var toggle = bg.gameObject.AddComponent<Toggle>(); toggle.isOn = initial; toggle.onValueChanged.AddListener(onChanged); toggle.graphic = check; toggle.targetGraphic = bg;
        toggle.onValueChanged.AddListener(on => { bg.color = on ? new Color(accent.r, accent.g, accent.b, 0.32f) : new Color(1,1,1,0.08f); check.rectTransform.anchoredPosition = on ? new Vector2(10,0) : new Vector2(-10,0); });
        toggle.onValueChanged.Invoke(toggle.isOn); return toggle;
    }
    void CreateCyclerRow(Transform p, string label, List<string> values, int startIndex, out Text valueText, System.Action<int> onChanged)
    {
        var row = CreateRow(p); CreateText("Label", row.transform, label, 18, FontStyle.Normal, TextAnchor.MiddleLeft);
        var left = CreateButton(row.transform, "◀", ()=>{}); left.gameObject.AddComponent<LayoutElement>().preferredWidth = 54;
        var display = CreateText("Value", row.transform, "", 18, FontStyle.Bold, TextAnchor.MiddleCenter); display.gameObject.AddComponent<LayoutElement>().preferredWidth = 240;
        var right = CreateButton(row.transform, "▶", ()=>{}); right.gameObject.AddComponent<LayoutElement>().preferredWidth = 54;
        int idx = Mathf.Clamp(startIndex, 0, Mathf.Max(0, values.Count-1));
        System.Action apply = () => { idx = (idx + values.Count) % values.Count; display.text = values[idx]; onChanged(idx); };
        left.onClick.AddListener(()=> { idx--; apply(); }); right.onClick.AddListener(()=> { idx++; apply(); });
        apply(); valueText = display;
    }

    // ============ Update para sprites animados ============
    static void Stretch(RectTransform rt){ rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero; }
    static void AnchorCenter(RectTransform rt){ rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.anchoredPosition = Vector2.zero; }
    static void SetSize(RectTransform rt, float w, float h){ AnchorCenter(rt); rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, w); rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h); }
    static void AddShadow(GameObject go, Color c, Vector2 dist, int size){ var shadow = go.AddComponent<Shadow>(); shadow.effectColor = c; shadow.effectDistance = dist; }
}

/// Pulso suave del color de fondo (para el tint overlay).
public class BackgroundColorPulser : MonoBehaviour
{
    public Color baseColor = new Color(0.05f, 0.06f, 0.08f, 0.88f);
    public float speed = 0.15f;
    public float alphaAmplitude = 0.05f;
    public float hueShift = 0.02f;

    Image img;
    void Awake(){ img = GetComponent<Image>(); if (!img) enabled = false; }
    void Update()
    {
        if (!img) return; float t = (Mathf.Sin(Time.time * speed) + 1f) * 0.5f;
        Color.RGBToHSV(baseColor, out float h, out float s, out float v);
        float h2 = Mathf.Repeat(h + (t - 0.5f) * hueShift, 1f);
        var c = Color.HSVToRGB(h2, s, v);
        c.a = Mathf.Clamp01(baseColor.a + (t - 0.5f) * 2f * alphaAmplitude);
        img.color = c;
    }
}
